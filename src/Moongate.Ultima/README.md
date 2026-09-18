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

## Client data and native dependencies

Provide your own Ultima Online client data when using the asset readers and configure its location through `Moongate.Ultima.Io.Files.SetDirectory`. Client assets are not included in this package.

The package depends on SkiaSharp, `SkiaSharp.NativeAssets.Linux.NoDependencies`, and `System.IO.Hashing`. NuGet resolves these dependencies. Rendering requires the appropriate native SkiaSharp runtime for the deployment platform; validate that runtime on the systems where your application will run.

This package has no dependency on another Moongate package. It provides client-data APIs, not a game server or a complete game client.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
