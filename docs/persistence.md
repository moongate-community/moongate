# Persistence and world saves

`Moongate.Persistence` stores MemoryPack entities in named binary collections.
Each collection has a snapshot and a write-ahead journal. The library works
without the game server; the host adds game-loop capture, autosave and retention.
See the [binary format](persistence-format.md) for headers and recovery rules.

## Define and register an entity

Reference `Moongate.Persistence` and `MemoryPack` 1.21.4 in a .NET 10 project.
Put this model in `Player.cs`:

```csharp
using MemoryPack;
using Moongate.Core.Attributes.Entities;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

[MemoryPackable(GenerateType.VersionTolerant), PersistenceCollection("players")]
public partial class Player : IMoongateEntity
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = string.Empty;
}
```

The application assigns a stable, nonzero `Serial`; persistence does not allocate
identities. Keep field orders stable and never reuse removed orders for a new
meaning. Version-tolerant serialization is not a schema migration system: test
old saves against new models and keep a backup before deploying model changes.
Register each entity type/collection once, before `InitializeAsync` begins.

## Explicit writes, queries and live saves

This complete `Program.cs` uses a new directory for each run and leaves it on disk
for inspection. The live-source list belongs only to this sequential example:

```csharp
using Moongate.Core.Primitives;
using Moongate.Persistence.Services;

var root = Path.Combine(Path.GetTempPath(), $"moongate-save-guide-{Guid.NewGuid():N}");
var storage = Path.Combine(root, "save");
var backup = Path.Combine(root, "backup");
var restored = Path.Combine(root, "restored");
var id = new Serial(1);
var live = new List<Player>();

await using (var persistence = new MoongatePersistenceService(storage))
{
    var players = persistence.Register<Player>("players", () => live);
    await persistence.InitializeAsync();
    var player = new Player { Id = id, Name = "Mario" };
    live.Add(player);
    await players.UpsertAsync(player);

    var detached = players.GetById(id) ?? throw new InvalidOperationException("Missing player");
    detached.Name = "Detached edit"; // Does not alter the committed record.
    if (players.GetById(id)?.Name != "Mario")
    {
        throw new InvalidOperationException("Read isolation failed");
    }

    player.Name = "Luigi";
    await persistence.SaveAllAsync(); // Captures the registered live list.
    var matches = await players.QueryAsync(p => p.Name.StartsWith("L", StringComparison.Ordinal));
    if (matches.Count != 1)
    {
        throw new InvalidOperationException("Query failed");
    }

    live.Clear();
    await persistence.SaveAllAsync(); // Omission is NOT deletion.
    if (players.GetById(id) is null)
    {
        throw new InvalidOperationException("Unexpected deletion");
    }

    await persistence.SaveAllWithBackupAsync(
        (capture, token) =>
        {
            token.ThrowIfCancellationRequested();
            capture();
            return Task.CompletedTask;
        },
        backup);
    await players.DeleteAsync(id); // Explicit, durable deletion in live storage.
}

await using (var reopened = new MoongatePersistenceService(storage))
{
    var players = reopened.Register<Player>("players");
    await reopened.InitializeAsync();
    if (players.GetById(id) is not null)
    {
        throw new InvalidOperationException("Deletion was not persisted");
    }
}

await MoongatePersistenceBackup.RestoreAsync(backup, restored);
await using (var recovery = new MoongatePersistenceService(restored))
{
    var players = recovery.Register<Player>("players");
    await recovery.InitializeAsync();
    if (players.GetById(id)?.Name != "Luigi")
    {
        throw new InvalidOperationException("Restore failed");
    }
}
Console.WriteLine($"Verified save, query, deletion and recovery in {root}");
```

`GetById`, `GetAll` and `QueryAsync` return detached objects from the committed
view. Query takes `Func<T, bool>` and returns `Task<IReadOnlyList<T>>`; it filters
an in-memory snapshot using ZLinq internally. There is no `IQueryable`, database
query translator or automatic property tracking. To save an edited read result,
call `UpsertAsync` on that instance.

| Operation | What it saves |
| --- | --- |
| `UpsertAsync(entity)` | One serialized entity, durably journaled |
| `DeleteAsync(id)` | Explicit deletion; returns whether the entity existed |
| `CheckpointAsync()` | Previously committed writes into snapshots; does not capture live sources |
| `SaveAllAsync()` | Registered live sources, then checkpoints every collection; collections without a source only checkpoint explicit writes |
| `SaveAllWithBackupAsync(...)` | A live save/checkpoint followed by a verified backup generation |

SaveAll is not a transaction across entities or collections. All live sources
are serialized before committing captured data, but cancellation or an I/O failure
during commit can leave earlier writes committed. Do not retry blindly after a
storage fault; inspect the error and reopen/validate the storage as appropriate.

## Container and world ownership

The server registers its persistence service before plugins and initializes it at
startup priority `-1000`. In a plugin's `Register(Container container)`, use:

```csharp
container.AddPersistenceEntity<Player>();
```

Import `Moongate.Persistence.Extensions`. This takes the collection name from
`[PersistenceCollection]` and registers the same singleton as both `DataAccess<Player>`
and `IDataAccess<Player>`. In a standalone container, first call
`container.RegisterMoongatePersistence(storage)` and explicitly initialize the
resolved `MoongatePersistenceService`.

For an application-owned world collection, register a live source instead:

```csharp
container.AddPersistenceEntity<Player>(() => livePlayers.Values);
```

Here `livePlayers` is your world's `Dictionary<Serial, Player>`. Register it before
startup, load persisted entities into it, and keep both enumeration and property
mutation on the game loop. Removing an entity from this dictionary does not delete
its persisted record: issue `DeleteAsync` as well, and prevent later captures from
reintroducing it. The host cannot discover every object implementing `IMoongateEntity`.

Outside the host, synchronize the entire capture, not just creation of the
`IEnumerable`. The callback overload of `SaveAllAsync` lets you dispatch the
supplied `Action` to the owner thread. Invoke it exactly once and await its actual
execution before returning. Do not call persistence mutations, another save,
checkpoint or disposal from a live source/capture; they can reenter the save gate.

## Host autosave and manual save

`WorldSaveService` activates after all services start and the started event has
been published. With the [default configuration](server-configuration.md), it
requests a save every 300 seconds, keeps five completed backup generations and
uses `<root>/save` for live data and `<root>/world-saves` for backups.

The service serializes live entities on the game loop and performs asynchronous
persistence work outside it. Concurrent save requests join one active save.
From an asynchronous host component **outside the loop**, inject
`Moongate.Server.Core.Interfaces.Services.IWorldSaveService` and await:

```csharp
await worldSave.SaveAsync(cancellationToken);
```

Cancellation stops that caller waiting; it does not cancel the shared save.
Do not use `.Wait()` or `.Result` from a packet handler or timer callback: the
save needs the game loop to perform capture. Save failures are logged and reported
to callers; an autosave is supervised rather than becoming an unobserved task.
There is currently no built-in `save` or `restore` console command.

`world_save.enabled = false` disables periodic requests only. Normal shutdown
after successful activation drains admitted work and captures once more before
world owners and persistence stop. Startup failure uses cleanup only; a faulted
loop cannot perform a successful final capture. Monitor shutdown/save errors.

## Backups and offline restore

A backup contains a manifest plus matching snapshot/journal files for the
registered collections. Publication uses a new directory; a failed publication
does not expose an incomplete generation under its final name. The live save may
already have committed even if backup publication fails. Retention applies only
to completed directories matching the host's `world-save-<timestamp>-<id>` naming
scheme; arbitrary folders are not pruned.

1. Stop the server and keep the existing root as a rollback copy. Never copy a
   changing live save directory and assume the files form one consistent backup.
2. Choose a completed backup containing `manifest.json`. Use
   `MoongatePersistenceBackup.RestoreAsync(backupPath, newDestination)` from a small
   .NET utility, as in the runnable example above. The destination must not exist
   or overlap the backup source. Lengths, hashes and checkpoint structure are verified.
3. Open the restored directory with the same entity registrations/models and check
   expected records. Restore validates binary structure; application-level model
   compatibility is checked when you initialize and read the collections.
4. With all server processes stopped, move the old `save` directory aside and put
   the validated restored directory at `<root>/save`. Preserve config, scripts and
   plugins separately; they are not included in persistence generations.
5. Restart and inspect load/save logs. Keep the old directory until recovery is
   confirmed. A backup on the same disk is not protection against disk loss;
   archive completed generations to your chosen independent storage.
