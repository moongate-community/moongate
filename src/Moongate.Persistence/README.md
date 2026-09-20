![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Persistence

Asynchronous PostgreSQL persistence for serial-identified entities, powered by FreeSql.

## Installation

Moongate.Persistence requires .NET 10 and PostgreSQL. Use the Moongate version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Persistence
```

## Features

- Explicit module ownership, PostgreSQL schemas, and Accounts or Realm database targets.
- Detached asynchronous reads, SQL-translated filtering, upserts, and deletes.
- Grouped writes in one asynchronous transaction for a single database target.
- Explicit schema preview/apply APIs with automatic synchronization disabled by default.
- Owner-controlled `SaveAllAsync` snapshots for application-managed live entities.

## Example

Define one stable attribute mapping in `Player.cs`. The application assigns every nonzero `Serial` identity.

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

Declare the entity's sole module owner in `PlayerModule.cs`.

<!-- nuget-smoke:PlayerModule.cs -->
```csharp
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

public sealed class PlayerModule : IPersistenceModule
{
    public string Id => "example.players";
    public string Schema => "sample_players";
    public PersistenceDatabaseTarget DatabaseTarget => PersistenceDatabaseTarget.Realm;
    public IReadOnlyCollection<Type> EntityTypes => [typeof(Player)];
}
```

`Program.cs` resolves its connection at runtime, applies the example schema explicitly, commits two writes together, and reads a detached value asynchronously. Set `MOONGATE_PERSISTENCE_DATABASE` to an Npgsql `key=value;` connection string for an empty development database before running it.

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
         .AddPersistenceModule<PlayerModule>()
         .AddPersistenceEntity<Player>();

await using var persistence = container.Resolve<MoongatePersistenceService>();
await persistence.InitializeAsync();
await persistence.ExecuteInTransactionAsync(PersistenceDatabaseTarget.Realm, async transaction =>
{
    var players = transaction.GetDataAccess<Player>();
    await players.UpsertAsync(new Player { Id = new Serial(1), Name = "Mario" });
    await players.UpsertAsync(new Player { Id = new Serial(2), Name = "Luigi" });
});

var player = await container.Resolve<IDataAccess<Player>>().GetByIdAsync(new Serial(1));
Console.WriteLine(player?.Name);
```

Normal deployments should keep automatic schema synchronization disabled. Review `PreviewSchemaAsync`, then run `SynchronizeSchemaAsync` with a separately authorized schema connection during maintenance.

## Behavior and scope

Reads return detached entities. Changing a returned instance does not persist it; call `UpsertAsync` or `DeleteAsync`. Writes are last-writer-wins and provide no optimistic concurrency token. A transaction callback covers one Accounts or Realm target and is never retried after an uncertain commit result.

`SaveAllAsync` captures registered live sources and commits one independent transaction per database target. Snapshot functions must deep-copy nested mutable state. An absent entity is retained; deletion is always explicit.

FreeSql can generate ordinary additive schema DDL. Use `OldName` for supported renames, and write explicit reviewed SQL for semantic data transformations. Downgrades are operator-managed. This package does not import the removed binary snapshot/journal format and does not create database backups.

Map every complex property explicitly with a supported column/navigation mapping or mark it for explicit omission, such as `IsIgnore`. Do not assume an ordinary writable object graph is serialized or cascaded automatically.

Mappings are immutable, attribute-only, and identical for a persistence CLR type everywhere. Modules select ownership and target; they do not remap types. Do not independently reconfigure these types through another raw FreeSql instance. Schema comparison has no migration history: it compares the current database with the current attributes and does not record the prior application model.

`FreeSql.Provider.PostgreSQL` 3.5.311 currently resolves Npgsql 5.0.18. This acknowledged provider limitation must not be hidden with a silent Npgsql major override. Upgrade the provider/driver combination only after running the PostgreSQL compatibility tests.

## Further reading

See the [persistence and operations guide](https://moongate.sh/server/persistence/) for schema review, plugin ownership, world saves, transactions, and database backup responsibility.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
