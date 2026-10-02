# Operate PostgreSQL: connections, roles and saves

This page is for the operator: how the server connects to its two databases, which
roles it needs, what a world save is, and what it is not. Entity mapping and data
access are in [Entities and data access](persistence.md); schema changes are in
[Migrations](persistence-migrations.md). The two databases and their target names
are listed in [Two databases, four names each](persistence.md#two-databases-four-names-each).

## Connections

Each database has one `connection_string`. It holds a PostgreSQL URI or a reference
to an environment variable. A newly generated configuration uses:

```toml
[persistence]
auto_sync_schema = false

[persistence.accounts]
connection_string = "postgres://moongate:moongate@localhost:5432/auth"

[persistence.realm]
connection_string = "postgres://moongate:moongate@localhost:5432/world"
```

These defaults are for local development. Existing TOML files are never rewritten.

For a deployment, replace the Accounts value with `"$MOONGATE_ACCOUNTS_DATABASE"`
and the Realm value with `"${MOONGATE_REALM_DATABASE}"`, and set each variable to a
URI such as `postgres://runtime:password@db:5432/moongate_realm?sslmode=require`
through your service manager or secret provider. Merely exporting those variables
does not override a literal URI in the file. `$NAME` and `${NAME}` references
expand once, without treating the result as a filesystem path or re-expanding the
substituted value. An undefined variable fails initialization for every configured
connection, including a database without entities or SQL files.

URI rules:

- Both `postgres://` and `postgresql://` are accepted. The port defaults to 5432.
  Bracket IPv6 hosts: `postgres://runtime@[::1]/moongate_realm`.
- Percent-encode reserved characters in usernames, passwords and database names:
  `@` becomes `%40`, `#` becomes `%23`, a literal `$` becomes `%24`. Values are
  decoded once; a `+` remains a literal plus.
- Query options include `sslmode`, `connect_timeout`, `application_name`,
  `search_path` and Npgsql option names. Unsupported options or SSL modes fail
  validation.
- Native Npgsql `key=value;` strings are also accepted, for direct library use.

## Startup checks

Normal startup opens each database and runs `SELECT 1`, regardless of server mode
or registered entities. A success logs `Postgres connection successful` with the
target and endpoint, without credentials. A connection or ping failure aborts
startup before other services start. Moongate does not create databases.

After the pings, startup validates the versioned SQL history of each active
database, including data-only migrations, and then compares the registered entity
mappings with the schema. Pending, changed or missing applied files, or an entity
that needs DDL no migration provides, prevent services from starting. Apply the
reviewed SQL with the migration runner while the server is stopped; see
[Migrations](persistence-migrations.md#generate-review-and-apply).

A connectivity ping does not activate entity mappings or migration checks for a
database that has nothing registered. Run the runner's `status --target ...`
during deployment to check the history of a database that has become empty. A
registered entity always activates its database and its migration checks.

`auto_sync_schema` defaults to false and should stay false. When true, startup
applies unversioned schema changes directly and records no migration history. Use
it only for a disposable development database.

## Separate DDL and runtime roles

Give normal processes a runtime connection. A one-shot schema job uses the same
`connection_string` setting with a schema-role URI for the same database. Its
separate TOML can reference a schema-only environment variable:

```toml
[persistence.realm]
connection_string = "$MOONGATE_REALM_SCHEMA_DATABASE"
```

Only that administrative process receives the schema credential; normal hosts
receive the runtime credential. There is no separate schema-connection setting in
server configuration.

The schema role owns the entity schemas and performs DDL. The runtime role needs
database `CONNECT`, entity-schema `USAGE`, `SELECT`, `INSERT`, `UPDATE` and
`DELETE` on entity tables, and `USAGE` on the entity sequences. Configure default
privileges for later plugin tables. For `moongate_migrations`, the runtime role
needs only schema `USAGE` and `SELECT` on `moongate_migrations.history`; never grant
it history writes. Set these grants as the owner after the first apply, or
pre-provision the schema and default `SELECT` privileges before it. Moongate does
not grant privileges silently. The
[one login and two game instances example](docker-login-realms.md) demonstrates
separate roles, grants and schema jobs.

## World saves

A world save captures the state the game loop owns and upserts it, one transaction
per active database. It is how the server persists what happened since the last
save; it does not delete rows that left the snapshot.

Periodic saves default to every 300 seconds:

```toml
[world_save]
enabled = true
interval_seconds = 300
```

### A save does not stop the game

Players keep walking, talking and fighting while the world is saved: no "the world is saving,
please wait", no frozen screen. A save runs in three steps, and only the first one holds the game
loop:

1. **Capture, on the loop.** Each live entity is copied into a detached snapshot, in memory. On a
   development world of 173,000 entities (144,000 items, 29,000 NPCs) this takes about 0.1 seconds,
   once every five minutes.
2. **Fingerprint, in the background.** Each snapshot gets a fingerprint, compared with the one of
   the last committed save; about a second for the same world, while the game goes on.
3. **Write, in the background.** Only the entities that changed are written to PostgreSQL, in one
   transaction per database. With the world quiet that is a few hundred rows instead of 173,000;
   every twelfth save (once an hour) writes everything again, about 14 seconds on that world, still
   in the background.

A save that fails rolls back as a whole and is retried in full by the next one; see
[Live world snapshots](persistence.md#live-world-snapshots) for the rules.

`enabled = false` disables the periodic request while keeping explicit and final
saves available. Concurrent requests join the active save; cancellation stops only
that caller's wait. An eligible shutdown runs a final capture after accepted work
drains, which is why Ctrl+C must be allowed to finish. A failed startup or a
faulted game loop cannot promise that final save, and a critical persistence
failure blocks later saves until the host is restarted; see
[Live world snapshots](persistence.md#live-world-snapshots) for that rule.

## Database backups

A world save is an application snapshot, not a database backup. For backups Moongate writes SQL
exports of the databases a process owns:

| Server mode | Files |
| --- | --- |
| `standalone` | `auth_<date>.sql` and `world_<date>.sql` |
| `login` | `auth_<date>.sql` |
| `game` | `world_<date>.sql` |

`<date>` is the UTC time of the backup, `yyyyMMdd_HHmmss`. The files go to `backups` under the
server root, and only the newest five of each database are kept. All of this is set in
[`[sql_backup]`](server-configuration.md).

A backup runs on the schedule when `sql_backup.enabled` is true, and at once with the
[`sql_backup`](commands/sql_backup.md) command.

### What a backup does

1. With the game role, it saves the world and waits for the save to finish. If the save fails, no
   file is written.
2. It exports each database in one read-only transaction, so a file is one consistent picture even
   when another save commits meanwhile.
3. It writes the file as `.tmp` and renames it when complete: a `.sql` file is always whole.
4. It deletes the oldest files of that database beyond `keep`.

The export runs off the game loop. Only the world save before it touches the loop.

A database that fails is logged and does not stop the other one; its older files are left alone.
Files in the directory that Moongate did not write are never deleted.

### What is in a file

Data only: a `TRUNCATE` of the tables, one `COPY ... FROM stdin` block for each table, and the
current value of each sequence, all in one transaction. The file holds no schema. Every table the
runtime role can read is included, also the tables of plugins. A table it cannot read is skipped
and named in a comment at the top of the file and in a log warning.

The migration history (`moongate_migrations`) is never in the file: it describes the schema of the
database it is in, and the restore procedure rebuilds it.

A file of `auth` holds the password hashes of the accounts. Keep the backup directory readable only
by the user that runs the server.

### Restore

Restore with the Moongate version that wrote the backup.

1. Stop the server.
2. Create an empty database and apply the migrations of that version with [`mgboot`](mgboot.md).
3. Run the file with a role that owns the tables:

   ```bash
   psql "postgres://moongate:<password>@localhost:5432/world" -v ON_ERROR_STOP=1 -f world_20261002_113000.sql
   ```

4. Start the server. To move to a newer version, upgrade after the restore: the newer migrations
   then run on the restored data as in any upgrade.

The file runs in one transaction: if it fails, the database is left as it was. Restore `auth` and
`world` from the same backup run, so characters and accounts match.

### What it does not do

The files are not compressed, not copied anywhere else and not restored by the server. Moongate has
no automatic reverse migration, so a file does not load onto an older schema, and a newer schema
may have columns the file cannot fill. For point-in-time recovery or off-site copies, use PostgreSQL's own tools
next to this.
