# Create Your First Plugin

This guide walks through the shortest path to a working C# plugin for Moongate.

You will:

1. install the plugin template
2. scaffold a new plugin project
3. add one simple command
4. package the plugin into a runtime-ready folder and zip
5. copy it into the server plugin directory

If you want the architecture and lifecycle details first, read [Plugin System](plugins.md).

## Prerequisites

Before you start, make sure you have:

- .NET SDK 10 or later
- access to the Moongate NuGet packages
- a local Moongate runtime directory

## 1. Install the Template

Install the template package:

```bash
dotnet new install Moongate.Templates::<version>
```

You only need to do this once per SDK environment. When a newer version is published, install that
version again to update the template.

## 2. Generate a New Plugin

Create a new plugin project:

```bash
dotnet new moongate-plugin \
  --name HelloPlugin \
  --pluginId hello-plugin \
  --authors "Squid" \
  --description "Example plugin"
```

This creates a folder like:

```text
HelloPlugin/
  HelloPlugin.csproj
  Plugin.cs
  manifest.json
  README.md
  data/
  scripts/
  assets/
```

If your plugin needs custom persisted entities, start with the persistence option enabled:

```bash
dotnet new moongate-plugin \
  --name HelloPlugin \
  --pluginId hello-plugin \
  --authors "Squid" \
  --description "Example plugin" \
  --withPersistence true
```

## 3. Inspect the Generated Files

The generated `Plugin.cs` already implements `IMoongatePlugin`.

The generated `manifest.json` already matches:

- the plugin id
- the plugin name
- the entry assembly path
- the entry type

That means you can start adding behavior immediately without building the plugin structure by hand.

## 4. Add a Minimal Command

Open `Plugin.cs` and register one command during `Configure(...)`.

Example:

```csharp
using Moongate.Plugin.Abstractions.Interfaces;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Internal.Commands;
using Moongate.Server.Abstractions.Interfaces.Services.Console;

namespace HelloPlugin;

public sealed class Plugin : IMoongatePlugin
{
    public string Id => "hello-plugin";

    public string Name => "HelloPlugin";

    public string Version => "1.0.0";

    public IReadOnlyList<string> Authors => ["Squid"];

    public string? Description => "Example plugin";

    public void Configure(IMoongatePluginContext context)
    {
        context.RegisterConsoleCommand<HelloPluginCommand>();
    }

    public Task InitializeAsync(
        IMoongatePluginRuntimeContext context,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }
}

[RegisterConsoleCommand("hello_plugin", "Prints a message from the plugin.")]
public sealed class HelloPluginCommand : ICommandExecutor
{
    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        context.Print("Hello from the plugin.");
        return Task.CompletedTask;
    }
}
```

Build the project:

```bash
dotnet build
```

## 5. Package the Plugin

The template generates packaging scripts for you.

On macOS or Linux:

```bash
bash scripts/pack-plugin.sh
```

On PowerShell:

```powershell
pwsh ./scripts/pack-plugin.ps1
```

This produces:

- `artifacts/hello-plugin/`
- `artifacts/hello-plugin.zip`

The runtime-ready folder will look like:

```text
artifacts/hello-plugin/
  manifest.json
  bin/
    HelloPlugin.dll
    HelloPlugin.deps.json
    ...
  data/
  scripts/
  assets/
```

## 6. Install the Plugin into Moongate

Copy the packaged folder into the runtime plugin directory:

```text
moongate_data/plugins/hello-plugin/
```

The final runtime layout should look like:

```text
moongate_data/plugins/hello-plugin/
  manifest.json
  bin/
    HelloPlugin.dll
    HelloPlugin.deps.json
    ...
```

If your runtime root is not the repository `moongate_data/` folder, use the equivalent
`plugins/<plugin-id>/` directory under your configured runtime root.

## 7. Start the Server

Start Moongate normally. The plugin loader will:

- discover `manifest.json`
- resolve plugin dependencies by id
- load the entry assembly
- run `Configure(...)`
- finish bootstrap
- run `InitializeAsync(...)`

If the plugin loads correctly, your command becomes available at startup.

## Common Problems

### Package version mismatch

Use the same version across the whole Moongate package chain:

- `Moongate.Plugin.Abstractions`
- `Moongate.Server.Abstractions`
- `Moongate.Persistence`
- `Moongate.UO.Data`

### Manifest and plugin class disagree

Keep these values aligned:

- `manifest.json.id` and `Plugin.Id`
- `manifest.json.name` and `Plugin.Name`
- `manifest.json.version` and `Plugin.Version`
- `manifest.json.entryType` and the fully-qualified plugin class name

### Plugin builds but does not load

Check the runtime folder layout first. The most common issue is copying only the DLL and not the
whole plugin folder with `manifest.json` and `bin/`.

## Next Steps

After the first plugin is working, move on to:

- [Plugin System](plugins.md) for lifecycle and dependency model details
- [Custom Persisted Entities](../persistence/custom-persisted-entities.md) if the plugin stores its own entities
- [Console Commands](../operations/console-commands.md) for command design patterns
