![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Core

Shared primitives, geometry, collections, configuration helpers, and utilities for Moongate applications.

## Installation

Requires .NET 10. Use the package version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Core
```

## Features

- Entity identities with `Serial` and the `IMoongateEntity` contract.
- Two- and three-dimensional points, rectangles, and geometry interfaces.
- Collections, buffers, and general-purpose helpers.
- TOML configuration helpers using snake_case names, version information, and network address utilities.

## Example

Create a position and a persistent entity identifier:

<!-- nuget-smoke:Program.cs -->
```csharp
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;

var position = new Point3D(100, 200, 5);
var id = new Serial(1);

Console.WriteLine($"{id}: {position.X}, {position.Y}, {position.Z}");
```

## Configuration, version and address recipes

Put this model in `ShardOptions.cs`:

```csharp
public sealed class ShardOptions
{
    public string ShardName { get; set; } = "Moongate";
    public int GamePort { get; set; } = 2593;
}
```

Run this `Program.cs` to round-trip a TOML file and inspect the calling assembly:

```csharp
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Moongate.Core.Utils;

var path = Path.Combine(Path.GetTempPath(), $"moongate-options-{Guid.NewGuid():N}.toml");
try
{
    await TomlUtils.SerializeToFileAsync(new ShardOptions(), path);
    var restored = await TomlUtils.DeserializeFromFileAsync<ShardOptions>(path)
        ?? throw new InvalidOperationException("Configuration was empty");
    Console.WriteLine(await File.ReadAllTextAsync(path)); // shard_name, game_port
    if (restored.ShardName != "Moongate" || restored.GamePort != 2593)
    {
        throw new InvalidOperationException("TOML round trip failed");
    }
}
finally
{
    File.Delete(path);
}

var assembly = Assembly.GetExecutingAssembly();
Console.WriteLine($"Application version: {VersionUtils.GetVersion(assembly)}");
Console.WriteLine($"Application codename: {VersionUtils.GetCodename(assembly)}");

var addresses = NetworkUtils.GetLocalIpAddresses()
    .Where(address => address.AddressFamily == AddressFamily.InterNetwork &&
                      !IPAddress.IsLoopback(address))
    .Distinct();
foreach (var address in addresses)
{
    Console.WriteLine(address);
}
```

`TomlUtils.Serialize`/`Deserialize<T>` work with strings; the file and async file
variants create parent directories when writing. Default property naming is
`snake_case`. Explicit `TomlSerializerOptions` replace the defaults for that call.
Writes overwrite the target and are not atomic; parsing, serialization and I/O
errors propagate to the caller. The server's create-default-if-missing behavior
belongs to its `ConfigHelper`, not to every `TomlUtils` write.

`VersionUtils.GetVersion()` without an assembly reads **Moongate.Core's** version.
Pass your application's assembly to read its version, stripping build metadata
following `+`. Codename reads the `AssemblyMetadata("Codename", "...")` value and
returns an empty string when absent. The server embeds its `Codename` MSBuild
property into this metadata through its project configuration; merely declaring
an arbitrary MSBuild property in another project does not embed it automatically.

`NetworkUtils.GetLocalIpAddresses()` enumerates local unicast addresses without
filtering loopback, inactive interfaces, duplicates or address family. Apply the
filters appropriate to your application, as above. These are local interface
addresses, not your public NAT address. `GetListeningAddresses(endpoint)` formats
local addresses of the endpoint's family with its port; it does not inspect the
OS socket table to discover actual listeners.

## Dependencies and scope

This package has no dependency on another Moongate package. Its external dependencies include DryIoc, Humanizer, Serilog, ShaiRandom, Tomlyn, and ZLinq; NuGet resolves them automatically.

It provides shared building blocks. Entity storage is provided by `Moongate.Persistence`; TCP transport is provided by `Moongate.Network`.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
