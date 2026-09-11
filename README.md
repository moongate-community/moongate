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
