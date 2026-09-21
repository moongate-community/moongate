![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Persistence

Asynchronous PostgreSQL persistence for serial-identified entities, powered by FreeSql.

## Installation

Moongate.Persistence requires .NET 10 and PostgreSQL. Use the Moongate version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Persistence
```

`InitializeAsync` opens and pings every configured runtime database with `SELECT 1`,
even when no entities use that target. Each success logs `Postgres connection successful`;
a failure throws before readiness. Connections are checked before migrations or schema
synchronization, and missing databases are never created automatically. The server
configures both Accounts and Realm in every mode; library integrations select their
own targets in `PostgreSqlPersistenceOptions`.

## Features

- Auth/World entity registration with automatic internal modules and PostgreSQL schemas.
- Detached asynchronous reads, SQL-translated filtering, upserts, and deletes.
- Grouped writes in one asynchronous transaction for a single database target.
- Versioned SQL readiness checks, draft schema preview, and explicit development synchronization.
- Opt-in development SQL generation with immutable files, isolated execution and persistent review gates.
- Owner-controlled `SaveAllAsync` snapshots for application-managed live entities.

## Example

Define one stable attribute mapping in `Player.cs`. Leave `Id` at zero for automatic assignment on the first `UpsertAsync`.

<!-- nuget-smoke:Player.cs -->

```csharp
using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

[Table(Name = "sample_players.players")]
public sealed class Player : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 100)]
    public string Name { get; set; } = "";
}
```

`Program.cs` resolves its connection at runtime, applies the example schema explicitly, commits two writes together, and
reads a detached value asynchronously. Set `MOONGATE_PERSISTENCE_DATABASE` to a `postgres://user:password@host:5432/database`
connection URI for an empty development database before running it.

<!-- nuget-smoke:Program.cs -->

```csharp
using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;

var connectionString = Environment.GetEnvironmentVariable("MOONGATE_PERSISTENCE_DATABASE")
    ?? throw new InvalidOperationException("Set MOONGATE_PERSISTENCE_DATABASE before running this example.");
var options = new PostgreSqlPersistenceOptions(
    [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, connectionString)],
    autoSynchronizeSchema: true);

using var container = new Container();
container.RegisterMoongatePersistence(options)
         .AddPersistenceWorld<Player>();

await using var persistence = container.Resolve<MoongatePersistenceService>();
await persistence.InitializeAsync();
var mario = new Player { Name = "Mario" };
await persistence.ExecuteInTransactionAsync(PersistenceDatabaseTarget.Realm, async transaction =>
{
    var players = transaction.GetDataAccess<Player>();
    await players.UpsertAsync(mario);
    await players.UpsertAsync(new Player { Name = "Luigi" });
});

var player = await container.Resolve<IDataAccess<Player>>().GetByIdAsync(mario.Id);
Console.WriteLine(player?.Name);
```

`AddPersistenceWorld<T>()` selects the Realm database; `AddPersistenceAuth<T>()`
selects the shared Accounts database. Moongate creates internal modules from the
schema-qualified table attributes, so no module class is required. Both helpers
also accept a live source and an explicit detached snapshot function. Explicit
`IPersistenceModule` declarations remain available for plugins that need them.

Connection options accept `postgres://` and `postgresql://` URIs, including
percent-encoded credentials, IPv6 hosts and query options such as `sslmode` and
`connect_timeout`. Native Npgsql connection strings are also supported.

Normal deployments keep automatic synchronization disabled. Generate and review versioned SQL, then apply it with the
separate `Moongate.MigrationRunner` executable. Configure `PostgreSqlPersistenceOptions.MigrationCatalogFactory` for
migration readiness checks in a custom host; Moongate.Server wires this automatically. `SynchronizeSchemaAsync` remains a
development-only convenience and does not record history.

## Behavior and scope

Reads return detached entities. Changing a returned instance does not persist it; call `UpsertAsync` or `DeleteAsync`. Writes
are last-writer-wins and provide no optimistic concurrency token. A transaction callback covers one Accounts or Realm target
and is never retried after an uncertain commit result.

A zero `Id` on `UpsertAsync` is assigned from a migration-managed PostgreSQL sequence
and written back through the entity's public `Id` setter. Nonzero IDs are preserved.
Sequences are per table, shared by processes, and bounded to the nonzero `uint` range;
they do not allocate UO mobile/item ranges. The runtime role needs sequence `USAGE`.
Failed inserts restore zero; a later transaction rollback retains an assigned ID.
Reservations are never reclaimed. New entities need no sequence names in their services.

`SaveAllAsync` requires already-assigned nonzero IDs; first persist new entities with
`UpsertAsync`. It captures registered live sources and commits one independent transaction per database target. Snapshot
functions must deep-copy nested mutable state. An absent entity is retained; deletion is always explicit.

FreeSql can generate ordinary additive schema DDL. Use `OldName` for supported renames, and write explicit reviewed SQL for
semantic data transformations. Downgrades are operator-managed. This package does not create database backups.

Map every complex property explicitly with a supported column/navigation mapping or mark it for explicit omission, such as
`IsIgnore`. Do not assume an ordinary writable object graph is serialized or cascaded automatically.

Mappings are immutable, attribute-only, and identical for a persistence CLR type everywhere. Modules select ownership and
target; they do not remap types. Do not independently reconfigure these types through another raw FreeSql instance. FreeSql
schema comparison compares the current database with current attributes. Versioned SQL and the checksum journal live in
`Moongate.Persistence.Migrations`; the separate runner applies them atomically.

`FreeSql.Provider.PostgreSQL` 3.5.311 currently resolves Npgsql 5.0.18. This acknowledged provider limitation must not be
hidden with a silent Npgsql major override. Upgrade the provider/driver combination only after running the PostgreSQL
compatibility tests.

## Further reading

See the [persistence and operations guide](https://moongate.sh/server/persistence/) for schema review, plugin ownership,
world saves, transactions, and database backup responsibility.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).

## Development migrations

The server supports `persistence.auto_generate_migrations = true` together with an
explicit `persistence.migrations_directory`. It writes and applies additive SQL at
startup; unsafe or unsupported changes remain marked for review. This mode is off
by default and cannot be combined with `auto_sync_schema`. Plain column indexes,
including unique and composite indexes, are automatic only on tables created in
the same migration. Indexes on existing tables and unsupported index expressions
or options require review.

Standalone library integrations can supply `DevelopmentMigrationOptions` with an
`IDevelopmentMigrationRunner` implementation and an explicit component resolver.
Keep migration execution isolated from FreeSql's PostgreSQL driver. See the
[development migration guide](https://moongate.sh/server/persistence/#automatic-development-migrations).
