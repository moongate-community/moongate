# C# Plugin System

Moongate supports startup-loaded C# plugins from the runtime `plugins/<plugin-id>/` directory.

If your local runtime root is the repository `moongate_data/` folder, the effective path becomes
`moongate_data/plugins/<plugin-id>/`.

This system is intentionally simple:

- plugins are loaded only at startup
- there is no unload or hot reload
- dependencies are resolved by plugin id
- plugins can register runtime contributions during bootstrap
- plugins can run lightweight initialization after the server is ready

## Plugin Folder Layout

Each plugin lives in its own directory:

```text
plugins/my-plugin/
  manifest.json
  bin/
    MyPlugin.dll
  data/
  scripts/
  assets/
```

Only `manifest.json` and the configured entry assembly are required.

## Authoring a Plugin Project

The minimum plugin project usually references `Moongate.Plugin.Abstractions`.

If the plugin registers commands, packet handlers, listeners, or file loaders, it will typically also
reference the Moongate assemblies that define those contracts.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Moongate.Plugin.Abstractions\Moongate.Plugin.Abstractions.csproj" />
    <ProjectReference Include="..\..\src\Moongate.Server\Moongate.Server.csproj" />
  </ItemGroup>
</Project>
```

After build, copy the plugin output to the runtime plugin folder so the final layout matches the manifest:

```text
plugins/my-plugin/
  manifest.json
  bin/
    MyPlugin.dll
    MyPlugin.deps.json
    MyPlugin.runtimeconfig.json
```

## Manifest

`manifest.json` declares the plugin metadata and entry point.

```json
{
  "id": "my-plugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "authors": ["Squid"],
  "description": "Adds custom server behavior.",
  "entryAssembly": "bin/MyPlugin.dll",
  "entryType": "MyPlugin.MyPlugin",
  "dependencies": [
    {
      "id": "moongate.dialogue",
      "versionRange": ">=1.0.0",
      "optional": false
    }
  ]
}
```

Rules:

- `id` must be unique
- required dependencies must exist
- dependency cycles fail startup
- plugins load in dependency order

## Lifecycle

Plugins implement `IMoongatePlugin` from `Moongate.Plugin.Abstractions`.

```csharp
using Moongate.Plugin.Abstractions.Interfaces;

namespace MyPlugin;

public sealed class MyPlugin : IMoongatePlugin
{
    public string Id => "my-plugin";

    public string Name => "My Plugin";

    public string Version => "1.0.0";

    public IReadOnlyList<string> Authors => ["Squid"];

    public string? Description => "Adds custom server behavior.";

    public void Configure(IMoongatePluginContext context)
    {
    }

    public Task InitializeAsync(
        IMoongatePluginRuntimeContext context,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }
}
```

The manifest and the plugin entry point should agree on `Id`, `Name`, and `Version`. The runtime uses
the manifest for discovery and dependency resolution, then instantiates the configured entry type.

### `Configure(...)`

`Configure(...)` runs before the final bootstrap wiring.

Use it to register:

- services
- packet handlers
- game event listeners
- console commands
- file loaders
- persistence descriptors
- Lua user data
- Lua script modules

### `InitializeAsync(...)`

`InitializeAsync(...)` runs after the runtime is ready.

Use it for lightweight startup work such as:

- resolving services
- reading plugin-local data
- initializing plugin caches

Avoid heavy world mutations here unless they are explicitly part of startup behavior.

## Service Access During Initialization

`InitializeAsync(...)` receives `IMoongatePluginRuntimeContext`, which exposes:

- `PluginId`
- `PluginDirectory`
- `Services`

`Services` is a small resolver wrapper. Use it to resolve already-bootstrapped runtime services:

```csharp
public Task InitializeAsync(
    IMoongatePluginRuntimeContext context,
    CancellationToken cancellationToken
)
{
    var commandService = context.Services.Resolve<ICommandSystemService>();
    _ = commandService;

    return Task.CompletedTask;
}
```

## Minimal Example

This example registers one console command.

```csharp
using Moongate.Plugin.Abstractions.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;

namespace MyPlugin;

public sealed class MyPlugin : IMoongatePlugin
{
    public string Id => "my-plugin";
    public string Name => "My Plugin";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Authors => ["Squid"];
    public string? Description => "Example plugin.";

    public void Configure(IMoongatePluginContext context)
    {
        context.RegisterConsoleCommand<HelloPluginCommand>();
    }

    public Task InitializeAsync(IMoongatePluginRuntimeContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

[RegisterConsoleCommand("hello_plugin", "Example plugin command.")]
public sealed class HelloPluginCommand : ICommandExecutor
{
    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        context.Print("Hello from plugin.");
        return Task.CompletedTask;
    }
}
```

## What Plugins Can Extend Today

The plugin bootstrap path can contribute to:

- service registration
- command registration
- packet handler wiring
- game event listener wiring
- file loader registration
- persistence descriptor registration
- Lua module and user-data registration

This keeps plugins aligned with the same runtime systems used by the built-in server.

## Dependency Resolution

Dependencies are declared in `manifest.json` and resolved by plugin `id`.

Startup fails when:

- two plugins declare the same `id`
- a required dependency is missing
- a dependency cycle exists
- a declared version requirement is not satisfied

Plugins are loaded, configured, and initialized in dependency order.
