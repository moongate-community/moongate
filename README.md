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

## Plugins

Plugins implement `IMoongatePlugin` from `Moongate.Server.Core`. Register plugin
instances explicitly after host services are configured and before
`bootstrap.StartAsync()`. Pass related plugins in one batch: dependencies are
validated and registered first, regardless of their position in the input.
There is no DLL discovery in this version.

For example, these three files define two plugins and a service. Each plugin
implementation project references `Moongate.Server.Core`.

`Plugins/ClockPlugin.cs`:

```csharp
using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;

namespace Moongate.PluginExamples.Plugins;

public sealed class ClockPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; }

    public ClockPlugin()
    {
        Metadata = new MoongatePluginData(
            "example.clock", "Clock", new Version(1, 2, 0), author: "Moongate");
    }

    public void Register(Container container)
    {
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
    }
}
```

`Services/GreetingService.cs`:

```csharp
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.PluginExamples.Services;

public sealed class GreetingService : IMoongateService
{
    private readonly TimeProvider _timeProvider;

    public GreetingService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Greet(string name)
    {
        return $"Hello, {name}! UTC time: {_timeProvider.GetUtcNow():O}";
    }
}
```

`Plugins/GreetingPlugin.cs`:

```csharp
using DryIoc;
using Moongate.PluginExamples.Services;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;

namespace Moongate.PluginExamples.Plugins;

public sealed class GreetingPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; }

    public GreetingPlugin()
    {
        Metadata = new MoongatePluginData(
            "example.greeting", "Greeting", new Version(1, 0, 0),
            description: "Registers the greeting service.",
            dependencies:
            [new MoongatePluginDependencyData("example.clock", new Version(1, 0, 0))]);
    }

    public void Register(Container container)
    {
        container.RegisterMoongateService<GreetingService>();
    }
}
```

A complete console composition example (`Program.cs`):

```csharp
using DryIoc;
using Moongate.PluginExamples.Plugins;
using Moongate.PluginExamples.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Plugins;

using var container = new Container();
container.RegisterMoongatePlugins(new GreetingPlugin(), new ClockPlugin());

foreach (var plugin in container.Resolve<MoongatePluginRegistry>().Plugins)
{
    Console.WriteLine($"{plugin.Id} {plugin.Version}");
}

Console.WriteLine(container.Resolve<GreetingService>().Greet("Moongate"));
```

The clock plugin is registered before the greeting plugin. Alternatively,
register prerequisites first with `container.RegisterMoongatePlugin<ClockPlugin>()`
and then `container.RegisterMoongatePlugin(new GreetingPlugin())`. The generic
form requires a public parameterless constructor; the instance form supports
explicit configuration.

IDs are case-insensitive. Dependencies are required; `MinimumVersion: null`
accepts any available version. A specified minimum version is inclusive.
Versions use `System.Version`, including its distinction between `1.2` and
`1.2.0`; use a consistent number of components. Prerelease labels and SemVer
ranges are not supported.

Duplicate IDs, missing dependencies, insufficient versions and dependency cycles
reject the entire batch before any `Register` callback. Correcting those inputs
allows another attempt. If a callback throws, registration stops and the registry
rejects further attempts. Services already registered cannot be rolled back:
abort startup and dispose the host container. The `using` scope above also
disposes the container on failure. Plugin registration must be serial and happen
before startup; nested registration is rejected.

Plugin callbacks only register services. Existing singleton behavior, lazy
factories and service registration metadata are preserved. Marking a service as
`IMoongateStartupService` records its autostart eligibility; the plugin registry
does not call its lifecycle methods. The host owns the container and service
lifecycle.

## License

MIT - see [LICENSE](LICENSE).
