# PostgreSQL persistence and world saves

Moongate stores registered entities in PostgreSQL through FreeSql. A deployment
uses one Accounts database and one database for each realm. Transactions and
world saves are independent across those targets; there is no cross-database
atomic commit.

For a complete first example, follow [Create a persistent entity](persistence-entity-tutorial.md).
It covers the entity class, registration, schema setup, and asynchronous reads and writes.

## Register entities

Every persisted type implements `IMoongateEntity` and has an application-assigned,
nonzero `Serial`. Select the database when registering it:

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

Use stable, explicit table and column names. Map `Serial` keys to PostgreSQL
`bigint` with `MapType = typeof(long)`. Ordinary public scalar properties are
mapped by FreeSql. Every complex property requires an explicit supported mapping,
navigation mapping, or explicit omission such as `IsIgnore`; do not assume an
unannotated complex object graph will be serialized or cascaded automatically.
Persistence mapping is immutable and attribute-only for a CLR type, and it must
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
reference to an environment variable:

```toml
[persistence]
auto_sync_schema = false

[persistence.accounts]
connection_string = "$MOONGATE_ACCOUNTS_DATABASE"

[persistence.realm]
connection_string = "${MOONGATE_REALM_DATABASE}"
```

Set each variable to a URI such as
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
variables fail only when their database target is activated. Targets without registered entities or installed SQL do not resolve a connection
or contact PostgreSQL. Use an explicit runner `status --target ...` during deployment to check history
even when a target has become completely empty; an inactive host target cannot
detect removal of its last data-only file without connecting. SQL-only targets
follow the configured login/game mode. A registered entity
always activates its target and migration checks, even when it uses the other role.

`FreeSql.Provider.PostgreSQL` 3.5.311 currently resolves Npgsql 5.0.18. This old
driver branch is an acknowledged provider limitation. Do not silently override
the Npgsql major version: upgrade the FreeSql provider as a compatible set and
run the PostgreSQL compatibility suite before deploying it.

`auto_sync_schema` defaults to false. Normal startup checks the versioned SQL
history, including data-only migrations, before comparing entity mappings with
PostgreSQL. Pending, changed, or missing applied files prevent services from
starting. Keep automatic schema synchronization only for disposable development
databases; it does not record versioned migrations.

### Versioned SQL files

```text
migrations/
  auth/0001_create_accounts.sql
  world/0001_create_characters.sql
plugins/MyPlugin/migrations/
  manifest.json
  world/0001_create_guilds.sql
```

The stock core directories are empty until core entities need tables. The sample
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
await items.UpsertAsync(new Item { Id = new Serial(1), Name = "Bandage" });

var item = await items.GetByIdAsync(new Serial(1));
var page = await items.QueryAsync(value => value.Name.StartsWith("B"), 0, 50);
await items.DeleteAsync(new Serial(1));
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
