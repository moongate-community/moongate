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

## Persistence

The server stores persistence files under `<root-directory>/save`. Register every
closed entity collection before persistence startup; server services and plugins
can then resolve `IDataAccess<T>`. The host initializes persistence before services
at the default priority and awaits its shutdown before disposing the DryIoc
container.

Entities are MemoryPack version-tolerant contracts. Keep every public entity in
its own file and assign explicit, stable member orders. ID allocation belongs to
the caller, and `Serial.Zero` is rejected.

`Entities/CharacterRecord.cs`:

```csharp
using MemoryPack;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.PersistenceExample.Entities;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class CharacterRecord : IMoongateEntity
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = "";

    [MemoryPackOrder(2)]
    public int Level { get; set; }
}
```

Register the owner and all collections, initialize once, and explicitly dispose
the asynchronous owner before synchronously disposing the container.

`Program.cs`:

```csharp
using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.PersistenceExample.Entities;

var rootDirectory = args.Length == 0 ? AppContext.BaseDirectory : args[0];
var options = new PersistenceOptions
{
    JournalCheckpointThresholdBytes = 64L * 1024 * 1024,
    MaxPayloadBytes = 16 * 1024 * 1024
};
var container = new Container();
container.RegisterMoongatePersistence(Path.Combine(rootDirectory, "save"), options)
    .RegisterDataAccess<CharacterRecord>("characters");

var persistence = container.Resolve<MoongatePersistenceService>();

try
{
    await persistence.InitializeAsync();
    var characters = container.Resolve<IDataAccess<CharacterRecord>>();
    var id = new Serial(0x40000001);

    await characters.UpsertAsync(new CharacterRecord { Id = id, Name = "Ada", Level = 24 });

    var loaded = characters.GetById(id);
    var veterans = await characters.QueryAsync(character => character.Level >= 20);

    if (loaded is not null)
    {
        loaded.Level++;
        await characters.UpsertAsync(loaded);
    }

    var deleted = await characters.DeleteAsync(id);
    Console.WriteLine($"Matches: {veterans.Count}; deleted: {deleted}");
}
finally
{
    try
    {
        await persistence.DisposeAsync();
    }
    finally
    {
        container.Dispose();
    }
}
```

`GetById`, `GetAll`, and `QueryAsync` return detached objects. Changing a returned
object does not change stored state; call `UpsertAsync` to persist it. `QueryAsync`
uses ZLinq internally over one captured committed view, but accepts the ordinary
`Func<T, bool>` shown above. There are no cross-collection transactions or indexes.

Each collection creates `characters.snapshot.bin`, `characters.journal.bin`, and
`characters.lock` in the configured save directory. Upserts and deletes are
acknowledged only after a durable journal flush. Startup replays the journal, and
healthy shutdown checkpoints it. Automatic checkpointing uses
`JournalCheckpointThresholdBytes`; compaction replaces recovery history and is not
an audit log, retention system, or backup.

Initialization fails closed for an unsupported format version, corrupt header,
checksum or payload, sequence errors, and when only one of the snapshot or journal
files exists. It does not silently reset committed files. An incomplete final
journal frame is the bounded recovery case and is truncated to its last complete
record. The exact layout and recovery rules are in the
[binary persistence format](docs/persistence-format.md).

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

Plugin callbacks register services and event subscriptions. Existing singleton
behavior, lazy factories and service registration metadata are preserved.
Marking a service as `IMoongateStartupService` records its autostart eligibility;
the plugin registry does not call its lifecycle methods. The host owns the
container and service lifecycle.

Plugins can also subscribe to host lifecycle events directly from `Register`.
An event-only plugin needs no dummy service registration:

```csharp
using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;

namespace Moongate.PluginExamples.Plugins;

public sealed class LifecyclePlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; }

    public LifecyclePlugin()
    {
        Metadata = new MoongatePluginData(
            "com.github.author.Moongate.plugins.lifecycle",
            "Lifecycle example",
            new Version(1, 0, 0),
            author: "Author"
        );
    }

    public void Register(Container container)
    {
        container.OnEvent<MoongateStartedEvent>(OnStartedAsync)
            .OnEvent<MoongateStoppingEvent>(OnStoppingAsync)
            .OnEvent<MoongateStoppedEvent>(OnStoppedAsync);
    }

    private static Task OnStartedAsync(
        MoongateStartedEvent message, CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }

    private static Task OnStoppingAsync(
        MoongateStoppingEvent message, CancellationToken cancellationToken
    )
    {
        // Save or flush resources here while startup services are still available.
        return Task.CompletedTask;
    }

    private static Task OnStoppedAsync(
        MoongateStoppedEvent message, CancellationToken cancellationToken
    )
    {
        // The stop phase is complete; the container and logger remain available here.
        return Task.CompletedTask;
    }
}
```

Server services inject `IEventBusService` and call its inherited `Subscribe` and
`PublishAsync` methods. Compose the shared bus and singleton adapter once in the
executable after logging is configured and before bootstrap starts:

```csharp
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Events;

container.RegisterMoongateEventBus()
    .RegisterMoongateService<IEventBusService, EventBusService>();
```

The injected service and plugin `OnEvent` registrations use the same
container-owned bus, so publications flow in both directions. A direct
`Subscribe` call returns an idempotent `IDisposable` token for early
unsubscription; otherwise the container owns the subscription lifetime.

Lifecycle events are transient and routed by exact event type; late subscribers
do not receive earlier events. Handlers run sequentially in registration order,
and the publisher awaits each returned task. One handler failure is logged and
does not skip later handlers. Publisher cancellation stops dispatch, while a
handler cancellation unrelated to the publisher is isolated like any other
handler failure. There is no automatic retry.

`OnEvent` subscriptions live until the host disposes its container. Use
`Stopping` for work that needs running services, including persistence writes.
`Stopped` runs after every service stop has been attempted and before container
and logging disposal; it reports completion of the stop phase even when a
service stop failed. Lifecycle callbacks must not await the host's own
`StartAsync` or `StopAsync`, because the host is already awaiting the callback.

## License

MIT - see [LICENSE](LICENSE).
