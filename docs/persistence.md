# PostgreSQL persistence and world saves

Moongate stores registered entities in PostgreSQL through FreeSql. A deployment
uses one Accounts database and one database for each realm. Transactions and
world saves are independent across those targets; there is no cross-database
atomic commit.

The old snapshot/journal backend is no longer a runtime option. See
[Migrating from binary persistence](persistence-format.md) before upgrading an
existing installation.

## Entities and module ownership

Every persisted type implements `IMoongateEntity`, has an application-assigned,
nonzero `Serial`, and belongs to exactly one `IPersistenceModule`. A module owns
one unique PostgreSQL schema in either the Accounts or Realm target.

```csharp
using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

[Table(Name = "inventory.items")]
public sealed class Item : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 100)]
    public string Name { get; set; } = "";
}

public sealed class InventoryModule : IPersistenceModule
{
    public string Id => "com.example.inventory";
    public string Schema => "inventory";
    public PersistenceDatabaseTarget DatabaseTarget => PersistenceDatabaseTarget.Realm;
    public IReadOnlyCollection<Type> EntityTypes => [typeof(Item)];
}
```

Use stable, explicit table and column names. Map `Serial` keys to PostgreSQL
`bigint` with `MapType = typeof(long)`. Ordinary public scalar properties are
mapped by FreeSql. Every complex property requires an explicit supported mapping,
navigation mapping, or explicit omission such as `IsIgnore`; do not assume an
unannotated complex object graph will be serialized or cascaded automatically.

Register modules and every entity they declare before schema preparation:

```csharp
container.RegisterMoongatePersistence(options)
         .AddPersistenceModule<InventoryModule>()
         .AddPersistenceEntity<Item>();
```

Registration performs no database I/O. Initialization validates the entire batch:
module IDs and schemas must be unique within their scopes, and every entity must
have exactly one declared and registered owner.

## Connections and schema preparation

The server TOML stores environment-variable names, never connection strings:

```toml
[persistence]
auto_sync_schema = false

[persistence.accounts]
connection_string_env = "MOONGATE_ACCOUNTS_DATABASE"

[persistence.realm]
connection_string_env = "MOONGATE_REALM_DATABASE"
```

Only targets with registered entities resolve their connection. Values use
Npgsql `key=value;` syntax, for example
`Host=db;Port=5432;Database=realm;Username=runtime;Password=...`. Supply them from
your service manager or secret provider.

`auto_sync_schema` defaults to false. Normal startup compares the registered
model with PostgreSQL and fails if DDL is required. Preview and apply changes with
the same plugin bundle and database endpoint as the runtime:

```sh
Moongate.Server --root-directory /srv/moongate/realm-1 --persistence-schema preview
Moongate.Server --root-directory /srv/moongate/realm-1 --persistence-schema apply
```

For framework-dependent output, run `dotnet Moongate.Server.dll ...`. Preview
prints generated DDL and changes nothing. Apply previews, acquires a per-target
PostgreSQL advisory lock, applies the DDL, and verifies that comparison is clean.
An unchanged preview reports `No PostgreSQL schema changes required.` These
administrative commands load real plugin registrations but do not acquire the
normal host PID, start listeners or startup services, or generate scripts,
certificates, and logs.

Stop the relevant game or login processes before applying reviewed DDL. The
advisory lock serializes schema operators; it does not pause runtime DML or own
the live game world. Keep automatic synchronization as an explicit development
opt-in for disposable databases, not the deployment default.

### Separate DDL and runtime roles

Give normal processes a runtime connection only. Give a one-shot schema job both
the runtime and schema connections, pointing to the same Host, Port, and Database:

```toml
[persistence.realm]
connection_string_env = "MOONGATE_REALM_DATABASE"
schema_connection_string_env = "MOONGATE_REALM_SCHEMA_DATABASE"
```

The schema role owns the module schema and performs DDL. The runtime role needs
database `CONNECT`, schema `USAGE`, and `SELECT`, `INSERT`, `UPDATE`, and `DELETE`
on tables. Configure default table privileges for the schema owner so later plugin
tables receive the same DML grants. Moongate does not grant privileges silently.
The [login and realms Compose guide](docker-login-realms.md) provides a complete
provisioning example.

## Schema evolution

Treat generated DDL as an operator-reviewed migration plan. FreeSql schema sync
handles ordinary model changes, but it cannot infer business meaning. Use its
`OldName` metadata for a supported table or column rename. Use separately reviewed,
versioned SQL for transformations such as splitting a value, backfilling rows,
changing units, merging tables, or enforcing a new invariant.

Test every migration on production-like data. Preview first, stop the affected
runtime, apply, verify, then start the new code. Downgrades are operator-managed: generated synchronization
does not promise a reverse migration or recover discarded data.

Disabling or uninstalling a plugin does not delete its schema. Preserve that data
until an operator deliberately archives or removes it.

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
container.AddPersistenceEntity<Item>(
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
