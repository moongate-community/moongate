# Migrations: generate, review and apply

Schema changes reach PostgreSQL as versioned SQL files. The server never applies
them: it validates at startup that every file in its catalog has been applied,
unchanged, and refuses to start otherwise. A separate executable, the migration
runner, applies them while the affected server is stopped.

This page covers the catalog layout, the commands, the rules for applied files, and
the optional development mode that generates SQL from entity changes. Entity
mapping is in [Entities and data access](persistence.md); connection strings and
roles are in [Operate PostgreSQL](persistence-operations.md). The two databases and
their target names are listed in
[Two databases, four names each](persistence.md#two-databases-four-names-each).

## Migration directory

On 0.6.0, both the server and the migration runner read the core SQL from the
`migrations/` directory beside the server executable; the runner also accepts
`--migrations-directory`. There is nothing to configure.

From the release after 0.6.0, the server reads `<root>/migrations` unless
`persistence.migrations_directory` selects another directory, and `mgboot` copies
the release's core SQL there and writes that absolute path into a new
configuration; see [Prepare a root with mgboot](mgboot.md). The runner uses
`--migrations-directory` if given, then the configured `migrations_directory`, then
the directory beside the server executable. Keep `migrations_directory` explicit so
the server and the runner read the same catalog.

## Versioned SQL files

```text
migrations/
  auth/0001_create_accounts.sql
  world/0001_create_characters.sql
plugins/MyPlugin/migrations/
  manifest.json
  world/0001_create_guilds.sql
```

Files use `NNNN_description.sql`: sequences `0001` through `9999`, lowercase
letters, digits and underscores in the description, no subdirectories. A sequence
is unique within one component and target. Non-SQL files in a target directory are
ignored.

A plugin ships its SQL inside its bundle with a `manifest.json` declaring a stable
component ID, independent of its bundle folder or CLR name:

```json
{ "id": "my-plugin" }
```

Use a unique lowercase ID of up to 63 letters, digits or hyphens, starting with a
letter; `core` is reserved. Never rename a component after applying its SQL.
Component ownership follows the loaded plugin bundle, not its C# namespace or
PostgreSQL schema.

Execution order is core first, plugins by ordinal component ID, then each
component's ascending sequence. There is no dependency solver: design cross-plugin
SQL around that order, or move shared changes into core. Auth and World have
independent catalogs and histories, even when configured on the same database.

## Generate, review and apply

Generate against a **reference database at the previous application version**.
A fresh empty database produces initial table creation, not an incremental change.
Load the same entity and plugin registrations as the version being developed:

```sh
Moongate.Server --root-directory /srv/moongate/reference --persistence-schema preview
Moongate.Server --root-directory /srv/moongate/reference \
  --persistence-schema generate --migration-target world \
  --migration-output ./migrations/world/0001_create_characters.sql
```

`preview` prints FreeSql's draft DDL. `generate` saves the selected target's draft
and refuses an existing file or an empty diff. Neither executes SQL or starts
host services, sockets, PID guards or certificates. Generation includes **all
registered modules for the selected target**. For plugin-specific SQL, use a
reference root containing only that plugin and required dependencies, and review
that the resulting file touches only schemas it owns. Registered modules in
other targets still need resolvable reference connections for schema comparison.

Review the SQL, add deliberate data transformations, test it, then commit the
file with the entity change. Deploy the same reviewed files to every affected
database. Stop its runtime processes and use the runner:

```sh
./migration-runner/Moongate.MigrationRunner status \
  --root-directory /srv/moongate/realm-1 --target world
./migration-runner/Moongate.MigrationRunner apply \
  --root-directory /srv/moongate/realm-1 --target world
```

The runner reads that root's `config/moongate.toml` and `plugins/`. `--target auth`
selects `[persistence.accounts]`; `--target world` selects `[persistence.realm]`.
Only the selected connection is resolved. `MOONGATE_ROOT` is an alternative to
`--root-directory`; without either, the shipped runner uses its parent server
directory as the data root. Released archives and Docker images ship the runner in
`migration-runner/`, isolated from FreeSql's Npgsql driver; it never loads plugin
DLLs. Both commands return exit code 0 on success and 1 on failure; `status`
reports pending files without applying them.

From a source checkout, provide the catalog path explicitly:

```sh
dotnet run --project src/Moongate.MigrationRunner -- status \
  --root-directory /srv/moongate/reference --target world \
  --migrations-directory ./migrations
```

Since 0.6.0, `Moongate.Server --persistence-schema apply` is not supported: it
directs you to the runner. For framework-dependent server output use
`dotnet Moongate.Server.dll ...`.

## Rules for applied files

[DbUp](https://dbup.readthedocs.io/en/latest/) executes the reviewed files. Moongate
adds a checksum journal in `moongate_migrations.history`, recording target,
component, filename, SHA-256 and application time. Checksums normalize CRLF to LF
and ignore a UTF-8 BOM, so a Windows checkout does not change a migration's identity.
Otherwise, **applied files are immutable**: never edit, rename or remove them.
Add a higher sequence for corrections. An installed component with a missing
applied file, a checksum mismatch or an inserted earlier sequence stops the runner
before new SQL executes. A removed plugin's history and data remain untouched.

Each apply acquires a database-wide PostgreSQL advisory lock on its execution
transaction, validates history, then applies **all pending scripts and journal
rows in one transaction**. SQL failure rolls the batch back. Concurrent runners
serialize, and a second apply becomes a no-op. Commands have a 60-second execution
timeout. A connection failure around commit can leave the result uncertain: inspect
`status` and the history before retrying. There is no transaction across Auth and
World or across realms. The advisory lock does not pause gameplay: stop affected
runtimes before a maintenance job.

Scripts must be transactional PostgreSQL SQL. Dollar-quoted `DO` blocks work;
DbUp variable substitution is disabled. Do not put `BEGIN`, `COMMIT`, `ROLLBACK`,
`SAVEPOINT`, `PREPARE`, `SET`, `RESET`, or other transaction or session control at
the top level. Use schema-qualified names instead of `SET search_path`. Commands
such as `CREATE INDEX CONCURRENTLY`, `VACUUM`, and psql meta-commands need separate
operator-managed procedures; the runner does not relax its transaction guarantee.

FreeSql compares the current attributed model with the current database; it cannot
infer business meaning or the previous application model. Use `OldName` for
supported renames and review its DDL. Write explicit SQL for backfills, value
splits, unit conversions, data merges and new invariants. Data-only files follow
the same numbering and history rules, and block normal startup until applied.

Test against representative data. Apply the reviewed files, confirm `status`
reports no pending changes and `preview` reports no schema changes, then start the
new server. Downgrades and nontransactional maintenance are operator-managed.
There is no automatic reverse migration and no database backup facility.

## The core auth catalog

The core auth catalog contains `0001_account_id_sequence.sql`, `0002_accounts.sql`
and `0003_account_serial_ownership.sql`. The third attaches the existing
`auth.account_id_seq` to `auth.accounts.id` without resetting its value; a
development-generated sequence that is already attached is retained. Existing
duplicate usernames, or null usernames or password hashes, must be resolved before
the constraint migration can apply; no account is silently deleted. The core world
catalog has no files yet. The sample plugin ships `world/0001_create_notes.sql`.

## Automatic development migrations

For a disposable development database, the server can generate and apply SQL from
entity changes at startup. Opt in explicitly:

```toml
[persistence]
auto_sync_schema = false
auto_generate_migrations = true
migrations_directory = "${MOONGATE_ROOT}/migrations"
```

Set `MOONGATE_ROOT` before starting the server, or use an absolute directory.
`migrations_directory` supports environment variables and `~`. You can point it at
your repository's `migrations/` directory so the generated files are ready to
commit. The directory is created if missing. Generation requires this explicit
path; it never silently writes to the build output directory. Keep
`auto_generate_migrations` disabled in deployment. It conflicts with
`auto_sync_schema`, which applies unversioned schema changes and records nothing.

### What a development start does

1. Check the configured PostgreSQL connections and validate migration checksums.
2. Apply pending, reviewed SQL through the isolated migration runner.
3. Compare registered entities, saving changes as `NNNN_auto_schema.sql` per target
   and component, without overwriting previous files.
4. Apply additive changes, recheck the database and then publish `PersistenceReady`.

### What generation handles

A new entity creates the initial migration, including its column-owned Serial
sequence. Existing tables without a sequence receive an additive migration that
initializes it above their highest stored ID. Adding a nullable property creates
the next migration. An unchanged restart creates no file. Required additions need
an explicit literal database default, for example
`[Column(IsNullable = false, DbType = "int4 NOT NULL DEFAULT 7")]` with an initial
property value of `7`. Generated null initialization and recognized constant
backfills are folded into the new-column DDL.

Plain column indexes, including unique and composite indexes with optional
`ASC`/`DESC` ordering, are applied automatically when their table is created in the
same generated migration. Adding an index to an existing table still requires
review, even if the table is empty, as do index expressions, predicates and other
unsupported index options.

### Drafts that need review

Renames, removals, existing-column alterations and unrecognized SQL produce drafts
marked with `-- moongate:review-required` and stop startup. The whole generated
batch for that target stays blocked, including on the next restart and in the
runner. Review and edit the **unapplied** SQL, remove the marker explicitly, and
restart to apply it. Never edit an already-applied migration. Table and entity
renames need explicit manual migrations: unrelated tables are retained, not
deleted. Commit the SQL files with their matching entity changes.

### Plugin SQL during development

Disk plugins keep SQL in `plugins/<Bundle>/migrations/auth/` or `world/` and must
provide their stable `migrations/manifest.json` ID. Internal application entities,
including `Moongate.Server.Ultima` entities, use the core migrations directory.
For plugin development, link or mount the source migration folder into the plugin
bundle.

### Locks, failures and baselines

Generation uses cooperative source-directory locks and the runner's PostgreSQL
advisory lock. Lock files named `.moongate-generation.lock` are retained; ignore
them in version control. Failed execution leaves the SQL file pending for a retry.
Canceled startup waits for the child process to exit and never announces
readiness; PostgreSQL may still be completing rollback, or a commit may already
have happened. On retry, migration history determines what remains to apply.
Core and plugin batches are transactional within one target only.

If a database already has entity tables from unversioned synchronization but no SQL
history for their component, create and review a baseline before enabling
generation. An ALTER-only first file would not recreate that database elsewhere.
Starting from an empty database avoids this step. Verify that the committed files
replay successfully on an empty database before deployment.

A custom source directory is not populated from the packaged SQL automatically. To
use the shipped auth catalog instead of generating the account table, copy all its
files before the first start. Never overwrite already-applied files or reuse their
numbers in an existing catalog.

The development source build copies the runner and its dependencies into a separate
`migration-runner/` output folder. A missing runner or an unwritable migration
directory prevents startup before generation can be reported successful.
