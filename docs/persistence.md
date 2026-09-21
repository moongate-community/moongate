# PostgreSQL persistence and world saves

Moongate stores registered entities in PostgreSQL through FreeSql. A deployment
uses one Accounts database and one database for each realm. Transactions and
world saves are independent across those targets; there is no cross-database
atomic commit.

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
variables fail only when their database target is activated. Targets without
registered entities do not resolve a connection or contact PostgreSQL.

`FreeSql.Provider.PostgreSQL` 3.5.311 currently resolves Npgsql 5.0.18. This old
driver branch is an acknowledged provider limitation. Do not silently override
the Npgsql major version: upgrade the FreeSql provider as a compatible set and
run the PostgreSQL compatibility suite before deploying it.

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

Moongate and FreeSql keep no migration history. Schema preview compares the
current attributed model with the current database; it is not a recorded version
sequence or a reliable detector of the previous application model.

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
