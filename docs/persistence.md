# PostgreSQL persistence: entities and data access

Moongate stores registered entities in PostgreSQL through FreeSql. A deployment
uses one Accounts database, shared by every server, and one Realm database per
world. Transactions and world saves never span those databases.

This page is for the developer who registers an entity and reads or writes it.
The other two persistence pages are
[Migrations: generate, review and apply](persistence-migrations.md), for changing
the schema, and [Operate PostgreSQL](persistence-operations.md), for connection
strings, roles, world saves and backups. For a complete first example, follow
[Create a persistent entity](persistence-entity-tutorial.md).

## Two databases, four names each

The same two databases appear under different names depending on where you meet
them. They are the same thing:

| Database | Purpose | TOML section | Migration target | Registration helper | C# target |
| --- | --- | --- | --- | --- | --- |
| Accounts | Shared login and account data | `[persistence.accounts]` | `--target auth`, `migrations/auth/` | `AddPersistenceAuth<T>()` | `PersistenceDatabaseTarget.Accounts` |
| Realm | This world's data | `[persistence.realm]` | `--target world`, `migrations/world/` | `AddPersistenceWorld<T>()` | `PersistenceDatabaseTarget.Realm` |

The generated defaults name the databases `auth` and `world`.

## Register entities

Every persisted type implements `IMoongateEntity` and has a stable, nonzero `Serial`.
Select the database when registering it:

```csharp
container.AddPersistenceAuth<Account>();     // Shared Accounts database.
container.AddPersistenceWorld<Character>();  // This realm's database.
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

Mapping rules:

- Map `Serial` keys to PostgreSQL `bigint` with `MapType = typeof(long)`. Do not mark
  the key `IsIdentity`; the sequence is attached by a migration.
- Column names default to lowercase snake_case: `Username` maps to `username`,
  `HashPassword` to `hash_password`, `CreatedAt` to `created_at`. Explicit
  `[Column(Name = "...")]` mappings take precedence and must also be lowercase snake_case.
- Ordinary public scalar properties are mapped by FreeSql. Every complex property
  needs an explicit supported mapping, a navigation mapping, or `IsIgnore`; an
  unannotated object graph is not serialized or cascaded.
- The mapping belongs to the CLR type and must be identical in every owner. A module
  selects ownership and database target; it cannot remap the type, and no code may
  reconfigure a persistence type through another raw FreeSql instance.

Register before schema preparation:

```csharp
container.RegisterMoongatePersistence(options)
         .AddPersistenceWorld<Item>();
```

Registration performs no database I/O. Initialization validates the entire batch,
including duplicate registrations, table mappings and schema ownership. Every type
has exactly one owner, and one CLR type cannot be registered in both databases in
the same container.

The server publishes `PersistenceReadyEvent` after initialization and before startup
services begin, and `PersistenceStoppedEvent` after the initialized owner is
disposed, before container disposal. Subscribe during plugin registration with
`container.OnEvent<TEvent>(...)`; see
[persistence lifecycle events](plugins.md#persistence-lifecycle-events). The
standalone persistence library does not publish these server events.

### Optional explicit plugin modules

A plugin can implement `IPersistenceModule` and register it through
`AddPersistenceModule<TModule>()` when it needs a stable module identifier and
an explicit list of owned entity types. It declares `Id`, `Schema`,
`DatabaseTarget` and `EntityTypes`. Register its entities with the matching
Auth/World helper, whose target must agree with the module, or with
`AddPersistenceEntity<T>()`, where the module supplies the target. Registration
order does not matter. An explicit module owns its complete schema: include every
registered entity in that schema in its declaration.

## Reads, writes and transactions

Resolve `IDataAccess<T>` for independent operations:

```csharp
var items = container.Resolve<IDataAccess<Item>>();
var item = new Item { Name = "Bandage" };
await items.UpsertAsync(item); // item.Id is assigned automatically.

var loaded = await items.GetByIdAsync(item.Id);
var page = await items.QueryAsync(value => value.Name.StartsWith("B"), 0, 50);
await items.DeleteAsync(item.Id);
```

`GetByIdAsync`, `GetAllAsync` and `QueryAsync` return detached values. Queries are
translated to SQL; an unsupported expression fails instead of switching to client
filtering. `GetAllAsync` is intentionally unbounded and meant for startup or
administration. Changing a returned object does not save it.

Upserts use last-writer-wins semantics. There is no optimistic concurrency token
and no automatic retry after an uncertain commit result. Group related writes on one
database with `ExecuteInTransactionAsync`:

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

## Automatic Serial assignment

Leave `Id` at zero on a new entity and `UpsertAsync` assigns it on that same
instance:

```csharp
var account = new AccountEntity { Username = "Mario", HashPassword = passwordHash };
await accounts.UpsertAsync(account);
Console.WriteLine(account.Id); // Assigned on this same instance.
```

- `Id == Serial.Zero` reserves a unique nonzero ID and performs an insert, never an
  update of an existing row. IDs already stored explicitly are skipped.
- A nonzero `Id` is preserved and uses the existing insert-or-update behavior.
- Automatic assignment requires a public `Id` setter. `IMoongateEntity` itself
  exposes only a getter.
- Each table has its own sequence in its schema. Sequences use the range
  `1..4294967295`, never cycle, and are shared across processes using that database.
  These are persistence IDs, not a UO mobile or item range allocator. Supply gameplay
  serials explicitly when domain rules require particular ranges or shared identity.
- Sequence creation belongs to schema migrations, never to `UpsertAsync`. The
  runtime role needs `USAGE` on the sequences as well as the table permissions.

A failed insert restores the entity's ID to zero. After a successful insert inside
a transaction, a later rollback keeps the assigned ID on the object; retrying
with that ID is an explicit upsert. Reservations are not rolled back or recycled,
so gaps are expected. Commit acknowledgement failures have an unknown durable
outcome and are not retried. Do not share or mutate an entity while its
persistence operation is running.

`SaveAllAsync` requires stable nonzero IDs on captured live entities: persist a new
entity with `UpsertAsync` before adding it to a live save collection. The advanced
`MoongatePersistenceService.ReserveSerialAsync<TEntity>("schema.sequence", cancellationToken)`
API remains available for explicitly migration-managed sequences; ordinary entities
do not need it. It does not create sequences or allocate gameplay serial ranges.

## Live world snapshots

For state owned by the game loop, register a source and a detached clone:

```csharp
container.AddPersistenceWorld<Item>(
    () => world.Items.Values,
    item => new Item { Id = item.Id, Name = item.Name });
```

The clone function must copy every mutable nested value; returning the live
instance is rejected. `SaveAllAsync` captures sources through the supplied owner
callback and later writes one transaction per active database. It upserts the
captured entities; absence from a snapshot is not deletion. Issue an explicit
`DeleteAsync` for removed rows.

Capture and any post-commit update of owner state must run on and finish through
the game loop, which owns that state. In the host, take
`IPersistenceOperationBarrier` around a critical operation and await both its
database work and its game-loop application. Posting a work item only waits for
admission; await the work item's own completion as well. See
[Game loop and timers](game-loop-and-timers.md) for admission and completion.

If an admitted critical callback fails or is canceled, the barrier keeps the
original cause and blocks queued and new critical operations plus later snapshots
and the final save. This prevents stale memory from overwriting a database commit
whose outcome may be uncertain. Shutdown still drains and stops, but the world
needs a fresh host before further persistence. The rule is conservative even when
the specific database transaction rolled back. Cancellation before admission and a
save-only failure do not poison the barrier.

How often saves run, and how the final save behaves at shutdown, is described in
[Operate PostgreSQL](persistence-operations.md#world-saves).

## Accounts

The built-in Ultima plugin registers `AccountEntity` in the Accounts database and
`IAccountService` in the container. `CreateAccountAsync` uses
`IDataAccess<AccountEntity>` and lets `UpsertAsync` assign the new account's `Id`.
`ListAccountsAsync` wraps `GetAllAsync`: an unbounded, detached snapshot of every
account, for administration rather than per-request lookups. `LoginAsync` verifies
the password hash and the lock state; no packet handler calls it yet.

The username index is case-sensitive, matching the service's lookup. Concurrent
attempts to register the same username return one success and
`UsernameAlreadyExists` for the others. Email is optional because creation does not
require one. Locked accounts cannot log in; canceled requests propagate
`OperationCanceledException`. The core auth SQL that creates this table is described
in [the core auth catalog](persistence-migrations.md#the-core-auth-catalog).

## Load testing

The [persistence stress scenario](persistence-stress.md) runs virtual sessions
through `DataAccess<T>` against an isolated PostgreSQL container, reports latency
and throughput, and verifies committed data after reopening persistence. It is
opt-in and needs no Ultima Online implementation or client files.
