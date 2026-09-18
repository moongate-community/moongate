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

## Dependencies and scope

This package has no dependency on another Moongate package. Its external dependencies include DryIoc, Humanizer, Serilog, ShaiRandom, Tomlyn, and ZLinq; NuGet resolves them automatically.

It provides shared building blocks. Entity storage is provided by `Moongate.Persistence`; TCP transport is provided by `Moongate.Network`.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
