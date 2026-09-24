![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Ultima

Ultima Online client data readers and rendering utilities for MUL/UOP assets, maps, art, and localization.

## Installation

Requires .NET 10. Use the package version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Ultima
```

## Features

- Readers for Ultima Online client data, including MUL and UOP resources.
- APIs for maps, art, gumps, animations, fonts, audio, and localization.
- Client file discovery and client version reading.
- Bitmap helpers and rendering support using SkiaSharp.

## Example

Create an in-memory surface and convert it to a caller-owned SkiaSharp bitmap. This example does not require UO client files.

<!-- nuget-smoke:Program.cs -->

```csharp
using Moongate.Ultima.Imaging;

using var bitmap = new UltimaBitmap(2, 2);
using var image = bitmap.ToImage();

Console.WriteLine($"{image.Width}x{image.Height}");
```

## Read client art, a map tile and localization

The following `Program.cs` accepts one client directory argument. Configure the
global file mapping **before** accessing static asset readers. Use one client
installation per process; changing `Files.SetDirectory` is not a coordinated
reload of every reader/cache already initialized.

```csharp
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;
using Moongate.Ultima.Localization;
using Moongate.Ultima.Maps;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    throw new ArgumentException("Pass an existing Ultima Online client directory");
}
var clientDirectory = Path.GetFullPath(args[0]);
Files.SetDirectory(clientDirectory);

// Land art is a 44x44 image in 16bppArgb1555. This buffer belongs to the caller.
var pixels = new ushort[44 * 44];
if (Art.TryGetLandPixels(0, pixels, out var patched))
{
    Console.WriteLine($"Land art decoded; patched: {patched}, pixels: {pixels.Length}");
}
else
{
    Console.WriteLine("Land art 0 is unavailable");
}

var mapPath = Files.GetFilePath("map0.mul") ?? Files.GetFilePath("map0LegacyMUL.uop");
if (mapPath is not null)
{
    // Use dimensions matching the client map; these are the modern Felucca defaults.
    // The implementation accepts null for Files mapping despite its non-null annotation.
    using var tiles = new TileMatrix(0, 0, 6144, 4096, path: null!);
    var tile = tiles.GetLandTile(0, 0);
    Console.WriteLine($"Map tile: {tile.Id}, Z: {tile.Z}");
}
else
{
    Console.WriteLine("Map 0 is unavailable");
}

var clilocPath = Files.GetFilePath("cliloc.enu");
if (clilocPath is not null)
{
    var strings = new StringList("enu", clilocPath, decompress: false);
    if (!string.IsNullOrEmpty(strings.LoadWarning))
    {
        Console.WriteLine(strings.LoadWarning);
    }
    Console.WriteLine(strings.GetString(3000000) ?? "Localization entry is unavailable");
}
else
{
    Console.WriteLine("English localization is unavailable");
}
```

Run with `dotnet run -- /absolute/path/to/client`. File lookup handles the known
client filenames case-insensitively. Some client versions omit assets or use a
different map size; check required inputs explicitly and surface parse/I/O failures.
The localization reader tries the alternate compression interpretation when
necessary and reports partially salvaged data through `LoadWarning`.

`Art.GetLand`/`GetStatic` offer bitmap APIs, but can return cache-owned objects;
do not dispose or mutate those as if they were private copies. The pixel API above
avoids that ownership ambiguity. A `TileMatrix` created by your application is
disposable and owns file handles. `UltimaBitmap` instances you construct and
`ToImage()` results you create are caller-owned and should be disposed, as in the
first example. These examples demonstrate reading; they do not modify client files.

## Client data and native dependencies

Provide your own Ultima Online client data when using the asset readers and configure its location through
`Moongate.Ultima.Io.Files.SetDirectory`. Client assets are not included in this package.

The package depends on SkiaSharp, `SkiaSharp.NativeAssets.Linux.NoDependencies`, and `System.IO.Hashing`. NuGet resolves
these dependencies. Rendering requires the appropriate native SkiaSharp runtime for the deployment platform; validate that
runtime on the systems where your application will run.

This package has no dependency on another Moongate package. It provides client-data APIs, not a game server or a complete
game client.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
