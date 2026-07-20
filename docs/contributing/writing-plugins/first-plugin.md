# Your first plugin

This walkthrough builds a complete plugin from an empty folder: a `greeter` config section
and a service that writes a configurable line to the log when the server starts. It touches
the two most common seams — a config section and a hosted service — and shows how to
reference the host assemblies, build, and deploy.

## Create the project

```bash
dotnet new classlib -n MyShard.Greeter -f net10.0
```

## Reference the host assemblies

A plugin loads into the *same* assembly context as the server, with no version isolation, so
it must be **compiled against the exact assemblies the server ships** — and must **not** carry
its own copies of them into `plugins/`. You get both by referencing the host DLLs with
`<Private>false</Private>`: the compiler sees them, but the build does not copy them next to
your plugin.

Point a property at your server build output and reference from there. In a source checkout
that is `src/Moongate.Server/bin/Release/net10.0/`; for a published server it is the folder
next to `Moongate.Server.dll`.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- Path to your Moongate server build output -->
    <MoongateBin>..\..\Moongate\src\Moongate.Server\bin\Release\net10.0</MoongateBin>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="SquidStd.Plugin.Abstractions">
      <HintPath>$(MoongateBin)\SquidStd.Plugin.Abstractions.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="SquidStd.Abstractions">
      <HintPath>$(MoongateBin)\SquidStd.Abstractions.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="DryIoc">
      <HintPath>$(MoongateBin)\DryIoc.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Serilog">
      <HintPath>$(MoongateBin)\Serilog.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

Later tutorials add one more reference — `Moongate.Server.Abstractions.dll` — for the
game-facing seams (commands, packet handlers, events, data loaders). The greeter needs none
of those.

## The config section

A config section is a plain class. Each property is one key; the class name has no bearing on
the section name — that is chosen at registration. Give every property a sensible default so
the plugin works before anyone edits `moongate.yaml`.

```csharp
namespace MyShard.Greeter.Data.Config;

/// <summary>Config for the greeter plugin (section <c>greeter</c> in moongate.yaml).</summary>
public sealed class GreeterConfig
{
    /// <summary>Line written to the log when the server starts.</summary>
    public string Message { get; set; } = "Hello from my first Moongate plugin!";
}
```

## The service

A hosted service implements `ISquidStdService`; the server calls `StartAsync` at boot and
`StopAsync` at shutdown. The bound `GreeterConfig` is injected directly — registering it as a
section (below) is what makes it resolvable.

```csharp
using Serilog;
using MyShard.Greeter.Data.Config;
using SquidStd.Abstractions.Interfaces.Services;

namespace MyShard.Greeter.Services;

public sealed class GreeterService : ISquidStdService
{
    private readonly ILogger _logger = Log.ForContext<GreeterService>();
    private readonly GreeterConfig _config;

    public GreeterService(GreeterConfig config)
    {
        _config = config;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("{Message}", _config.Message);

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
```

See [Hosted services](hosted-service.md) for background loops, shutdown, and the
"stay optional" rule.

## The plugin class

`Configure` registers the section and the service. `Metadata.Id` must be unique and stable —
it is how other plugins depend on yours and how the loader reports it.

```csharp
using DryIoc;
using MyShard.Greeter.Data.Config;
using MyShard.Greeter.Services;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace MyShard.Greeter;

public sealed class GreeterPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "myshard.greeter",
            Name = "MyShard Greeter",
            Version = new Version(1, 0, 0),
            Author = "you",
            Description = "Logs a greeting when the server starts"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        container.RegisterConfigSection<GreeterConfig>("greeter");
        container.RegisterStdService<GreeterService, GreeterService>();
    }
}
```

To track the assembly's version instead of hard-coding it, use
`Version = new(VersionUtils.GetVersion(typeof(GreeterPlugin).Assembly))` from
`SquidStd.Core.Utils` (add a `<Reference>` to `SquidStd.Core.dll`).

## Build, deploy, run

```bash
dotnet build -c Release
cp bin/Release/net10.0/MyShard.Greeter.dll <server-root>/plugins/
```

Only `MyShard.Greeter.dll` lands in `plugins/` — the host assemblies stayed out because they
are `<Private>false</Private>`. Add the section to `moongate.yaml`:

```yaml
greeter:
  message: "Welcome to my shard"
```

Start the server. You will see the loader pick the plugin up, then the greeting:

```
Loaded plugin myshard.greeter v1.0.0 by you
Welcome to my shard
```

## Next

- [Hosted services](hosted-service.md) — do real background work in your service.
- [Registration reference](registration.md) — every other seam `Configure` can call.
