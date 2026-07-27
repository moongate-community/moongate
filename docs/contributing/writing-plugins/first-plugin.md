# Your first plugin

This walkthrough builds a complete plugin from an empty folder: a `greeter` config file
and a service that writes a configurable line to the log when the server starts. It touches
two of the most common seams — a config file and a hosted service — and shows how to
reference the assemblies, build, and deploy.

## Create the project

```bash
dotnet new classlib -n MyShard.Greeter -f net10.0
```

## Reference the assemblies

A plugin loads into the *same* assembly context as the server — there is **no version
isolation** — so it must be built against the **same** assembly versions the server runs.

### PackageReference (recommended)

The greeter only uses SquidStd, DryIoc and Serilog, which are on NuGet.org. Reference the SquidStd
packages at the **same version the server ships** (check the server's release notes /
`Directory.Build.props` — currently `0.38.0`); DryIoc and Serilog come with them:

```xml
<ItemGroup>
  <PackageReference Include="SquidStd.Plugin.Abstractions" Version="0.38.0" />
  <PackageReference Include="SquidStd.Abstractions" Version="0.38.0" />
  <PackageReference Include="SquidStd.Core" Version="0.38.0" />
</ItemGroup>
```

Plugins that use Moongate's **game-facing seams** (commands, packet handlers, events, data
loaders — the later tutorials) additionally reference **`Moongate.Server.Abstractions`**, which
Moongate publishes to GitHub Packages. Add a repo-local `nuget.config` for the feed, authenticated
with a GitHub token that has the `read:packages` scope — GitHub Packages requires authentication
even for public packages:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="moongate" value="https://nuget.pkg.github.com/moongate-community/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <moongate>
      <add key="Username" value="%GITHUB_USER%" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </moongate>
  </packageSourceCredentials>
</configuration>
```

```xml
<PackageReference Include="Moongate.Server.Abstractions" Version="0.5.0" />
```

Match the version to the server you target; the Moongate packages are available from the first
release after this feature ships, and a single reference to `Moongate.Server.Abstractions` pulls
the rest of the closure transitively.

### HintPath to a local build (fallback)

When you build against a source checkout of the server, skip the feed and reference the host DLLs
directly with `<Private>false</Private>` — the compiler sees them, the build does not copy them
into `plugins/`, and the versions match because they *are* the server's. Point a property at your
server build output (`src/Moongate.Server/bin/Release/net10.0/` in a source checkout):

```xml
<PropertyGroup>
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
  <Reference Include="SquidStd.Core">
    <HintPath>$(MoongateBin)\SquidStd.Core.dll</HintPath>
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
```

Add `Moongate.Server.Abstractions.dll` the same way for the game-facing seams. Either way, only
your plugin's own `.dll` ships into `<root>/plugins/`.

## The config

The config is a plain class. Each property is one key; the class name has no bearing on
the section name — that is chosen at registration. Give every property a sensible default so
the plugin works before anyone edits the config file.

```csharp
namespace MyShard.Greeter.Data.Config;

/// <summary>Config for the greeter plugin (section <c>greeter</c>, in its own file).</summary>
public sealed class GreeterConfig
{
    /// <summary>Line written to the log when the server starts.</summary>
    public string Message { get; set; } = "Hello from my first Moongate plugin!";
}
```

## The service

A hosted service implements `ISquidStdService`; the server calls `StartAsync` at boot and
`StopAsync` at shutdown. The bound `GreeterConfig` is injected directly — registering it
(below) is what makes it resolvable.

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
using SquidStd.Core.Directories;
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
        var directories = container.Resolve<DirectoriesConfig>();
        container.RegisterConfigFile<GreeterConfig>("greeter", directories["plugins/configs"]);
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

Only `MyShard.Greeter.dll` lands in `plugins/` — the assemblies you referenced are the host's to
resolve (or `<Private>false</Private>` in the local-build fallback). Because the plugin registers
its config with `RegisterConfigFile`, the server generates `<root>/plugins/configs/greeter.yaml`
with the defaults at first start, then binds and reloads it (embedded plugins keep a section in
`moongate.yaml` instead — see [Per-plugin config file](registration.md#per-plugin-config-file)):

```yaml
# plugins/configs/greeter.yaml
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
