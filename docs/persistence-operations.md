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

`enabled = false` disables the periodic request while keeping explicit and final
saves available. Concurrent requests join the active save; cancellation stops only
that caller's wait. An eligible shutdown runs a final capture after accepted work
drains, which is why Ctrl+C must be allowed to finish. A failed startup or a
faulted game loop cannot promise that final save, and a critical persistence
failure blocks later saves until the host is restarted; see
[Live world snapshots](persistence.md#live-world-snapshots) for that rule.

## Database backups

A world save is an application snapshot, not a database backup. Moongate does not
create, restore, retain or coordinate PostgreSQL backups, and has no automatic
reverse migration. Backup policy belongs to the operator and is independent for
Accounts and for each realm.
