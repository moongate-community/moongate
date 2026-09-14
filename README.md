# Moongate

## Overview

Short description goes here.

## Build

```bash
dotnet build Moongate.slnx
```

## Test

```bash
dotnet test Moongate.slnx
```

## Publish server

Publish a self-contained executable for Linux x64:

```bash
dotnet publish src/Moongate.Server/Moongate.Server.csproj -c Release -r linux-x64
./src/Moongate.Server/bin/Release/net10.0/linux-x64/publish/Moongate.Server
```

The publish directory contains only `Moongate.Server`, including the .NET runtime
and managed dependencies. Native libraries included in the bundle are extracted
on first launch. Release debug symbols are embedded in the assemblies, and XML
API documentation is omitted from the publish output.

Publish separately for each operating system and architecture: replace
`linux-x64` with `win-x64`, `linux-arm64`, `osx-arm64`, or another supported RID.
Windows produces `Moongate.Server.exe`. `dotnet build` keeps its normal development
output; single-file packaging happens during `dotnet publish`.

Build and run the container with the same executable:

```bash
docker build -f src/Moongate.Server/Dockerfile -t moongate .
docker run --rm -it moongate
```

## Image processing

`Moongate.Ultima` uses SkiaSharp. The project includes
`SkiaSharp.NativeAssets.Linux.NoDependencies` for Linux image processing without
Fontconfig; native Windows and macOS assets are supplied by SkiaSharp.

`UltimaBitmap` retains its native ARGB1555 pixel buffer. `ToImage()` returns a
caller-owned `SKBitmap` that must be disposed; `FromImage(SKBitmap)` leaves the
source bitmap owned by the caller. Imports use an alpha threshold of 128.
Animation and multi coordinates use `SKPointI`; hue colors use `SKColor`.

`Save()` selects PNG, JPEG (`.jpg` or `.jpeg`), or WebP from the filename extension,
case-insensitively. Other output formats, including BMP and TIFF, throw
`NotSupportedException`. Use `opaque: true` for RGB555 surfaces such as map renders.
PNG streams returned by the rendering facades start at position zero and must be
disposed by the caller. `FromFile()` accepts formats supported by Skia's decoder
(including PNG, JPEG, WebP and BMP); TIFF input is unsupported.

## License

MIT - see [LICENSE](LICENSE).
