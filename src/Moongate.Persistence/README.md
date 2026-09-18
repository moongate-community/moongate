![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Persistence

Binary snapshot and journal persistence for serial-identified entities, powered by MemoryPack.

## Installation

Requires .NET 10 and a writable storage directory. Use the Moongate version available in your configured NuGet feed. Reference MemoryPack directly in applications that define serializable entities so their source-generator requirement is explicit.

```shell
dotnet add package Moongate.Persistence
dotnet add package MemoryPack --version 1.21.4
```

## Features

- Typed `DataAccess<T>` collections for entities implementing `IMoongateEntity`.
- Explicit upsert and delete operations recorded in a binary journal.
- Snapshots, journal recovery, checkpoints, and backups.
- Queries using ZLinq and `SaveAllAsync` support, including opt-in live entity sources.

## Example

Define the entity in `Player.cs`. Keep MemoryPack field order stable when evolving stored data.

<!-- nuget-smoke:Player.cs -->
```csharp
using MemoryPack;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class Player : IMoongateEntity
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = string.Empty;
}
```

Save and reload an entity in `Program.cs`. This example owns a temporary directory and removes it after both persistence instances are disposed.

<!-- nuget-smoke:Program.cs -->
```csharp
using Moongate.Core.Primitives;
using Moongate.Persistence.Services;

var directory = Path.Combine(Path.GetTempPath(), $"moongate-example-{Guid.NewGuid():N}");

try
{
    await using (var persistence = new MoongatePersistenceService(directory))
    {
        var players = persistence.Register<Player>("players");
        await persistence.InitializeAsync();
        await players.UpsertAsync(new Player { Id = new Serial(1), Name = "Mario" });
    }

    await using (var persistence = new MoongatePersistenceService(directory))
    {
        var players = persistence.Register<Player>("players");
        await persistence.InitializeAsync();
        Console.WriteLine(players.GetById(new Serial(1))?.Name);
    }
}
finally
{
    if (Directory.Exists(directory))
    {
        Directory.Delete(directory, recursive: true);
    }
}
```

## Dependencies and scope

This package depends on `Moongate.Core`, MemoryPack, and ZLinq. Entity identifiers use `Serial`.

Property assignments are not automatically tracked. Persist changes through `UpsertAsync` or `DeleteAsync`. Read APIs return detached entities; changing a returned instance does not save it. Register a live entity source when you want `SaveAllAsync` to capture application-owned entities.

Autosave scheduling belongs to the host application. This package does not provide an automatic schema-migration framework; plan format changes and backups before changing deployed entity models.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
