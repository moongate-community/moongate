# PostgreSQL persistence and world saves

Moongate stores registered entities in PostgreSQL through FreeSql. A deployment
uses one Accounts database and one database for each realm. Transactions and
world saves are independent across those targets; there is no cross-database
atomic commit.

For a complete first example, follow [Create a persistent entity](persistence-entity-tutorial.md).
It covers the entity class, registration, schema setup, and asynchronous reads and writes.

## Register entities

Every persisted type implements `IMoongateEntity` and has a stable, nonzero `Serial`.
For a new entity, leave `Id` at zero: `UpsertAsync` assigns it automatically. Select the database when registering it:

```csharp
container.AddPersistenceAuth<Account>();     // Shared Accounts/login database.
container.AddPersistenceWorld<Character>();  // This realm's world database.
```

Moongate creates internal modules automatically, grouping entities by database
and PostgreSQL schema. A separate module class is not required. Declare a stable,
schema-qualified table name in the entity's attributes:

```csharp
using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

[Table(Name = "inventory.items")]
public sealed class Item : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 100)]
    public string Name { get; set; } = "";
}
```

Use explicit table names and stable column names. Map `Serial` keys to PostgreSQL
`bigint` with `MapType = typeof(long)`. Ordinary public scalar properties are
mapped by FreeSql. Every complex property requires an explicit supported mapping,
navigation mapping, or explicit omission such as `IsIgnore`; do not assume an
unannotated complex object graph will be serialized or cascaded automatically.

Column names default to lowercase snake_case: `Username` maps to `username`,
`HashPassword` to `hash_password`, and `CreatedAt` to `created_at`. Explicit
`[Column(Name = "...")]` mappings take precedence and must also use lowercase
snake_case. Keep the schema-qualified `[Table(Name = "...")]` and identity
mapping explicit.

Persistence mapping uses this fixed convention and attributes for a CLR type, and it must
be identical in every owner. A module selects ownership and database target; it
cannot remap the type. Application and plugin code must not reconfigure a
persistence type through another raw FreeSql instance.

Register before schema preparation:

```csharp
container.RegisterMoongatePersistence(options)
         .AddPersistenceWorld<Item>();
```

Registration performs no database I/O. Initialization validates the entire batch,
including duplicate registrations, table mappings and schema ownership.

The server publishes `PersistenceReadyEvent` after initialization and before startup
services begin. It publishes `PersistenceStoppedEvent` after the initialized owner
is disposed, before container disposal. Subscribe during plugin registration using
`container.OnEvent<TEvent>(...)`; see [persistence lifecycle events](plugins.md#persistence-lifecycle-events)
for timing, failure behavior, and examples. The standalone persistence library
does not publish these server events.

### Optional explicit plugin modules

A plugin can still implement `IPersistenceModule` and register it through
`AddPersistenceModule<TModule>()` when it needs a stable module identifier and
an explicit list of owned entity types. It declares `Id`, `Schema`,
`DatabaseTarget`, and `EntityTypes`. Register its entities with the matching
Auth/World helper; the helper's target must agree with the module. Registration
order does not matter. `AddPersistenceEntity<T>()` is available for this explicit
module pattern, where the module supplies the target.

An explicit module owns its complete schema: include every registered entity in
that schema in its declaration. All other Auth/World registrations get automatic
internal modules. Every type has exactly one owner, and one CLR type cannot be
registered in both databases in the same container.

## Connections and schema preparation

Each database has one `connection_string`. It can contain a PostgreSQL URI or a
reference to an environment variable. A newly generated server configuration uses:

```toml
[persistence]
auto_sync_schema = false

[persistence.accounts]
connection_string = "postgres://moongate:moongate@localhost:5432/auth"

[persistence.realm]
connection_string = "postgres://moongate:moongate@localhost:5432/world"
```

The `auth` and `world` databases must already exist. Normal startup checks both
with `SELECT 1`, regardless of server mode or registered entities. A successful
ping logs `Postgres connection successful` for its target; any failure aborts
startup before other services start. Databases are not created automatically.
These defaults are for local development, and existing TOML files remain unchanged.

To use environment variables, replace the Accounts `connection_string` with
`"$MOONGATE_ACCOUNTS_DATABASE"` and the Realm value with
`"${MOONGATE_REALM_DATABASE}"`. Set each variable to a URI such as
`postgres://runtime:password@db:5432/moongate_realm?sslmode=require` through your
service manager or secret provider. A literal URI is also supported by
`connection_string`; keep real credentials in your secret provider. Both
`postgres://` and `postgresql://` are accepted. The port defaults to 5432; bracket
IPv6 hosts, for example `postgres://runtime@[::1]/moongate_realm`.

Percent-encode reserved characters in URI usernames, passwords and database names:
`@` becomes `%40`, `#` becomes `%23`, and a literal `$` becomes `%24`. URI query
options include `sslmode`, `connect_timeout`, `application_name`, `search_path`,
and Npgsql option names. Values are decoded once; a `+` remains a literal plus.
Unsupported options or SSL modes fail validation. Native Npgsql `key=value;`
strings are also accepted for direct library integrations.

`$NAME` and `${NAME}` references expand once, without treating the result as a
filesystem path or re-expanding characters in the substituted value. If a URI
contains individual placeholders, supply URI-encoded component values. Undefined
variables fail during initialization for every configured runtime connection,
including targets without entities or SQL files. Direct library users choose the
configured targets in `PostgreSqlPersistenceOptions`; the server always configures
both. Construction and registration remain free of database I/O.

Connection checks do not activate entity mappings or migrations for an otherwise
inactive target. Use an explicit runner `status --target ...` during deployment to
check history even when a target has become completely empty: a connectivity ping
does not detect removal of its last data-only file. SQL-only migration targets
follow the configured login/game mode. A registered entity always activates its
target and migration checks, even when it uses the other role.

`FreeSql.Provider.PostgreSQL` 3.5.311 currently resolves Npgsql 5.0.18. This old
driver branch is an acknowledged provider limitation. Do not silently override
the Npgsql major version: upgrade the FreeSql provider as a compatible set and
run the PostgreSQL compatibility suite before deploying it.

`auto_sync_schema` defaults to false. Normal startup checks the versioned SQL
history, including data-only migrations, before comparing entity mappings with
PostgreSQL. Pending, changed, or missing applied files prevent services from
starting. Keep automatic schema synchronization only for disposable development
databases; it does not record versioned migrations.

### Automatic development migrations

To save and apply SQL migrations when the server starts, opt in explicitly:

```toml
[persistence]
auto_sync_schema = false
auto_generate_migrations = true
migrations_directory = "${MOONGATE_ROOT}/migrations"
```

Set `MOONGATE_ROOT` before starting the server, or use an absolute directory.
`migrations_directory` supports environment variables and `~`. The example saves
core SQL under `$MOONGATE_ROOT/migrations/auth/` and `world/`. You can instead point
it at your repository's `migrations/` directory so the generated files are ready
to commit. The directory is created if missing. Generation requires this explicit
path; it never silently writes to the build output directory.

Keep `auto_generate_migrations` disabled in deployment. It conflicts with
`auto_sync_schema`, which applies unversioned schema changes. With generation
turned off, startup still validates SQL history and the entity schema without
applying pending migrations. The standalone runner honors the configured
`migrations_directory`; its `--migrations-directory` argument takes precedence.

The development startup sequence is:

1. Check configured PostgreSQL connections and validate migration checksums.
2. Apply pending, reviewed SQL through the isolated migration runner.
3. Compare registered entities, saving changes as `NNNN_auto_schema.sql` per target
   and component, without overwriting previous files.
4. Apply additive changes, recheck the database and then publish `PersistenceReady`.

A new entity creates the initial migration, including its column-owned Serial
sequence. Existing tables without a sequence receive an additive migration that
initializes it above their highest stored ID. Previously applied SQL remains unchanged. Adding a nullable property creates the
next migration. An unchanged restart creates no file. Supported required additions
need an explicit literal database default, for example
`[Column(IsNullable = false, DbType = "int4 NOT NULL DEFAULT 7")]` with an initial
property value of `7`. Unsupported types, expressions and backfill shapes require
review. Generated null initialization and recognized constant backfills are folded
into the new-column DDL, avoiding unnecessary row updates and UPDATE triggers.

Plain column indexes (including unique and composite indexes, with optional
`ASC`/`DESC` ordering) are applied automatically when their table is created in the
same generated migration. Adding an index to an existing table still requires
review, even if the table is empty. Index expressions, predicates and other
unsupported index options also require review.

Renames, removals, existing-column alterations and unrecognized SQL produce drafts
marked with `-- moongate:review-required` and stop startup. The whole generated batch
for that target remains blocked, including on the next restart and in the standalone
runner. Review and edit the **unapplied** SQL, then remove that marker explicitly.
Restart to apply it. Do not edit any already-applied migration. Table/entity renames
need explicit manual migrations: unrelated tables are retained, not automatically
deleted. Commit the SQL files with their matching entity changes.

Disk plugins keep SQL in `plugins/<Bundle>/migrations/auth/` or `world/` and must
provide their stable `migrations/manifest.json` ID. Internal application entities,
including `Moongate.Server.Ultima` entities, use the core migrations directory.
For plugin development, link or mount the source migration folder into the plugin
bundle. Component ownership follows the loaded plugin bundle, not its C# namespace
or PostgreSQL schema.

Generation uses cooperative source-directory locks and the runner's PostgreSQL
advisory lock. Lock files named `.moongate-generation.lock` are retained and should
be ignored by version control. Failed execution leaves the SQL file pending for a
retry. Canceled startup waits for the child process to exit and never announces
readiness; PostgreSQL may still be completing rollback, or a commit may already have
happened. On retry, migration history determines what remains to apply.

Core and plugin batches are transactional **within one target**. Auth and world are
independent databases; there is no transaction spanning both.

If a database already has entity tables from unversioned synchronization but no SQL
history for their component, create and review a baseline before enabling generation.
An ALTER-only first file would not recreate that database elsewhere. For a disposable
local database, starting with an empty database avoids this baseline step. Verify
that the committed files replay successfully on an empty database before deployment.

The development source build copies the runner and its dependencies into a separate
`migration-runner/` output folder. Release bundles already ship an isolated runner.
The host never loads the runner's PostgreSQL driver into FreeSql's dependency graph.
A missing runner or unwritable migration directory prevents startup before generation
can be reported successful.

### Versioned SQL files

```text
migrations/
  auth/0001_create_accounts.sql
  world/0001_create_characters.sql
plugins/MyPlugin/migrations/
  manifest.json
  world/0001_create_guilds.sql
```

The core auth catalog ships the account ID sequence and accounts table migrations;
the core world catalog is currently empty. The sample
plugin already ships `world/0001_create_notes.sql`. A plugin manifest declares a
stable migration component ID, independent of its bundle folder or CLR name:

```json
{ "id": "my-plugin" }
```

Use a unique lowercase ID of up to 63 letters, digits or hyphens, starting with a
letter; `core` is reserved. Never rename a component after applying its SQL.
Files use `NNNN_description.sql`: sequences `0001` through `9999`, lowercase
letters, digits and underscores in the description, no subdirectories. A sequence
is unique within one component and target.

Execution order is core first, plugins by ordinal component ID, then each
component's ascending sequence. There is no dependency solver: design cross-plugin
SQL around that order, or move shared changes into core. Auth and World have
independent catalogs and histories, even when configured on the same database.
Normally Auth uses one shared database and each realm uses its own World database.

### Generate, review and apply

Generate against a **reference database at the previous application version**.
A fresh empty database produces initial table creation, not an incremental change.
Load the same entity/plugin registrations as the version being developed:

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
database. Stop its runtime processes and use the separate runner:

```sh
./migration-runner/Moongate.MigrationRunner status \
  --root-directory /srv/moongate/realm-1 --target world
./migration-runner/Moongate.MigrationRunner apply \
  --root-directory /srv/moongate/realm-1 --target world
```

The runner reads that root's existing `config/moongate.toml` and `plugins/`.
`--target auth` selects `[persistence.accounts]`; `--target world` selects
`[persistence.realm]`. Only the selected connection is resolved. `MOONGATE_ROOT`
is an alternative to `--root-directory`. Without either override, the shipped
runner uses its parent server directory as the data root. Released binaries and Docker images
include the runner in `migration-runner/`, isolated from FreeSql's Npgsql driver.
It never loads plugin DLLs. Both commands return exit code 0 on success and 1 on
failure; status reports pending files without applying them.

Core SQL defaults to the `migrations/` directory beside the server executable.
From a source checkout, provide its path explicitly:

```sh
dotnet run --project src/Moongate.MigrationRunner -- status \
  --root-directory /srv/moongate/reference --target world \
  --migrations-directory ./migrations
```

The server uses its shipped catalog; the runner's optional directory override is
for authoring and maintenance. Deploy identical SQL to the server before restart.
`Moongate.Server --persistence-schema apply` is no longer supported: it directs
you to the versioned runner. For framework-dependent server output use
`dotnet Moongate.Server.dll ...`.

### Separate DDL and runtime roles

Give normal processes a runtime connection. A one-shot schema job uses the same
`connection_string` setting with a schema-role URI for the same database. Its
separate TOML can reference a schema-only environment variable:

```toml
[persistence.realm]
connection_string = "$MOONGATE_REALM_SCHEMA_DATABASE"
```

Only that administrative process receives the schema credential; normal hosts
receive the runtime credential. There is no separate schema-connection setting
in server configuration.

The schema role owns the entity schemas and performs DDL. The runtime role needs
database `CONNECT`, entity-schema `USAGE`, and `SELECT`, `INSERT`, `UPDATE`, and
`DELETE` on entity tables. Configure default privileges for later plugin tables.
For `moongate_migrations`, runtime needs only schema `USAGE` and `SELECT` on
`moongate_migrations.history`; never grant it history writes. Set these grants as
the owner after the first apply, or pre-provision the schema and default SELECT
privileges before apply. Moongate does not grant privileges silently. The
[Compose guide](docker-login-realms.md) demonstrates separate roles and grants.

## Schema evolution

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
`status`/history before retrying. There is no transaction across Auth and World or
across realms. The advisory lock does not pause gameplay: stop affected runtimes
before a maintenance job.

Scripts must be transactional PostgreSQL SQL. Dollar-quoted `DO` blocks work;
DbUp variable substitution is disabled. Do not put `BEGIN`, `COMMIT`, `ROLLBACK`,
`SAVEPOINT`, `PREPARE`, `SET`, `RESET`, or other transaction/session control at the
top level. Use schema-qualified names instead of `SET search_path`. Commands such
as `CREATE INDEX CONCURRENTLY`, `VACUUM`, and psql meta-commands require separate
operator-managed procedures; the runner does not relax its transaction guarantee.

FreeSql still compares the current attributed model with the current database;
it cannot infer business meaning or the previous application model. Use `OldName`
for supported renames and review its DDL. Write explicit SQL for backfills, value
splits, unit conversions, data merges and new invariants. Data-only files follow
the same numbering and history rules, and block normal startup until applied.

Test against representative data. Apply the reviewed files, confirm `status`
reports no pending changes and schema `preview` reports no changes, then start the
new server. Downgrades and nontransactional maintenance are operator-managed.
There is no automatic reverse migration or database backup facility.

## Reads, writes, and transactions

Resolve `IDataAccess<T>` for independent operations:

```csharp
var items = container.Resolve<IDataAccess<Item>>();
var item = new Item { Name = "Bandage" };
await items.UpsertAsync(item); // item.Id is assigned automatically.

var loaded = await items.GetByIdAsync(item.Id);
var page = await items.QueryAsync(value => value.Name.StartsWith("B"), 0, 50);
await items.DeleteAsync(item.Id);
```

`GetByIdAsync`, `GetAllAsync`, and `QueryAsync` return detached values. Queries
are translated to SQL; an unsupported expression fails instead of switching to
client filtering. `GetAllAsync` is intentionally unbounded and is intended for
startup or administration. Changing a returned object does not save it.

Upserts use last-writer-wins semantics. There is no optimistic concurrency token
or automatic retry after an uncertain commit result. Group related writes on one
target with `ExecuteInTransactionAsync`:

```csharp
await persistence.ExecuteInTransactionAsync(
    PersistenceDatabaseTarget.Realm,
    async transaction =>
    {
        var items = transaction.GetDataAccess<Item>();
        await items.UpsertAsync(first);
        await items.UpsertAsync(second);
    });
```

The callback must complete asynchronously and must not escape its transaction
facade. A transaction cannot span Accounts and Realm. If a workflow changes both,
design explicit compensation or reconciliation.

## Live world snapshots

For state owned by the game loop, register a source and a detached clone:

```csharp
container.AddPersistenceWorld<Item>(
    () => world.Items.Values,
    item => new Item { Id = item.Id, Name = item.Name });
```

The clone function must copy every mutable nested value. Returning the live
instance is rejected. `SaveAllAsync` captures sources through the supplied owner
callback and later writes one transaction per active database target. It upserts
the captured entities; absence from a snapshot is not deletion. Issue an explicit
`DeleteAsync` for removed rows.

Capture and any post-commit update of owner state must run on and finish through
the owner loop. In the host, take `IPersistenceOperationBarrier` around a critical
operation and await its database work and owner-loop application. Posting a work
item only waits for admission; await the work item's own completion as well.

If an admitted critical callback fails or is canceled, the barrier keeps the
original cause and blocks queued/new critical operations plus later snapshots and
the final save. This prevents stale RAM from overwriting a database commit whose
outcome may be uncertain. Shutdown still drains and stops, but the world needs a
fresh host/reload before further persistence. The rule is conservative even when
the specific database transaction rolled back. Cancellation before admission and
a save-only failure do not poison the barrier.

Periodic world saves default to every 300 seconds. Concurrent requests join the
active save; cancellation stops only that caller's wait. Eligible shutdown runs a
final capture after accepted work drains. `world_save.enabled = false` disables
periodic requests while keeping explicit and final saves available.

## Database backups

World save is an application snapshot operation, not a database backup. Moongate
does not create, restore, retain, or coordinate PostgreSQL backups. Database
backup policy belongs to the operator and is independent for Accounts and each
realm.


## Account IDs and registration

The built-in Ultima plugin registers `AccountEntity` in the auth database and
`IAccountService` in the container. `CreateAccountAsync` uses `IDataAccess<AccountEntity>`
without allocating IDs itself. `UpsertAsync` fills in the new account's `Id`.

The core auth catalog contains `0001_account_id_sequence.sql`, `0002_accounts.sql`
and `0003_account_serial_ownership.sql`. The third migration attaches the existing
`auth.account_id_seq` to `auth.accounts.id` without resetting its value. If a
development-generated sequence is already attached, it is retained. Apply the
catalog with `Moongate.MigrationRunner apply --target auth` before running the new
server version. Stop writers while applying schema changes.

With automatic development migrations enabled, a fresh custom source directory
can generate the account table, index and sequence directly from the registered
entity. Custom directories are not populated from packaged SQL automatically.
To use the shipped auth catalog instead, copy all its files before first startup.
Never overwrite already-applied files or reuse their numbers in an existing catalog.

The username index is case-sensitive, matching the service's existing lookup
behavior. Concurrent attempts to register the same username return one success
and `UsernameAlreadyExists` for the other attempts. Existing duplicate usernames
or null usernames/password hashes must be resolved before the constraint migration
can apply; no account is silently deleted. Email is optional because creation does
not require one. Locked accounts cannot log in; canceled requests propagate
`OperationCanceledException`.

## Automatic Serial assignment

```csharp
var account = new AccountEntity { Username = "Mario", HashPassword = passwordHash };
await accounts.UpsertAsync(account);
Console.WriteLine(account.Id); // Assigned on this same instance.
```

- `Id == Serial.Zero` reserves a unique nonzero ID and performs an insert, never an
  update of an existing row. IDs already stored explicitly are skipped.
- A nonzero `Id` is preserved and uses the existing insert-or-update behavior.
- Automatic assignment requires a public `Id` setter. Keep the `bigint` mapping;
  do not mark it as `IsIdentity`. `IMoongateEntity` itself still exposes only a getter.
- Each table has its own sequence in its schema. Sequences use the range
  `1..4294967295`, never cycle, and are shared across processes using that database.
  These are persistence IDs, not a UO mobile/item range allocator. Supply gameplay
  serials explicitly when domain rules require particular ranges or shared identity.
- Sequence creation belongs to schema migrations (or explicit development schema
  synchronization), never to `UpsertAsync`. The runtime role needs `USAGE` on the
  sequences as well as the normal table permissions. The migration role creates
  the sequence and must be able to attach it to the table it owns.

A failed insert restores the entity's ID to zero. After a successful insert inside
a transaction, a later rollback retains the assigned ID on the object; retrying
with that ID is an explicit upsert. Reservations are not rolled back or recycled,
so gaps are expected. Commit acknowledgement failures still have an unknown durable
outcome: operations are not automatically retried. Do not share/mutate an entity
while its persistence operation is running.

`SaveAllAsync` still requires stable nonzero IDs on captured live entities. Persist
a new entity with `UpsertAsync` before adding it to the live save collection.

The advanced `MoongatePersistenceService.ReserveSerialAsync<TEntity>("schema.sequence",
cancellationToken)` API remains available for explicitly migration-managed sequences.
It is no longer required by account services or ordinary new entities. It does not
create sequences or allocate gameplay serial ranges.
