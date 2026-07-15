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

## Documentation

The documentation site lives in `docs/` and is built with [DocFX](https://dotnet.github.io/docfx/):

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

Then browse http://localhost:8080. The site is published to GitHub Pages on every push to `main`.

## License

MIT - see [LICENSE](LICENSE).
