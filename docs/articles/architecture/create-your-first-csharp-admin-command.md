# Create Your First C# Admin Command

This guide adds a compiled command to a Moongate plugin.

By the end, you will:

- create one `ICommandExecutor` class
- register it from `Plugin.Configure(...)`
- package the plugin
- test the command from the server console and in game

## Before You Start

Finish this guide first:

- [Create Your First Plugin](create-your-first-plugin.md)

This tutorial assumes you already have a generated plugin project.

The examples below use a plugin project named `HelloPlugin`, because that is the project name used in
[Create Your First Plugin](create-your-first-plugin.md).

## Step 1: Add The Command Class

Inside your plugin project, create:

```text
HelloPlugin/TutorialPingCommand.cs
```

Paste this content:

```csharp
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace HelloPlugin;

[RegisterConsoleCommand(
    "tutorial_ping_cs",
    "Tutorial plugin command that prints a message.",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class TutorialPingCommand : ICommandExecutor
{
    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        context.Print("tutorial_ping_cs executed.");

        return Task.CompletedTask;
    }
}
```

What this does:

- `RegisterConsoleCommand` gives the command its name, description, source, and minimum account level
- `CommandSourceType.Console | CommandSourceType.InGame` allows both server-console and in-game usage
- `AccountType.GameMaster` keeps it in the admin/operator bucket

## Step 2: Register The Command In The Plugin

Open:

```text
HelloPlugin/Plugin.cs
```

Update `Configure(...)` so it registers the command:

```csharp
public void Configure(IMoongatePluginContext context)
{
    context.RegisterConsoleCommand<TutorialPingCommand>();
}
```

## Step 3: Build The Plugin

From the plugin project root, run:

```bash
dotnet build
```

This verifies that the command compiles with the plugin.

## Step 4: Package The Plugin

From the same plugin root, run:

```bash
bash scripts/pack-plugin.sh
```

Expected output:

- `artifacts/hello-plugin/`
- `artifacts/hello-plugin.zip`

## Step 5: Copy The Plugin Into The Shard Root

Copy the packaged folder into:

```text
~/moongate/plugins/hello-plugin/
```

The final runtime layout should include:

```text
~/moongate/plugins/hello-plugin/
  manifest.json
  bin/
  data/
  scripts/
  assets/
```

## Step 6: Restart The Server

Restart the server so it loads the updated plugin package.

## Step 7: Test The Command

From the server console, run:

```text
tutorial_ping_cs
```

Then, if you are logged in with a GameMaster-capable account, run in game:

```text
.tutorial_ping_cs
```

Expected result in both cases:

- the command prints `tutorial_ping_cs executed.`

## Common Mistakes

- Creating the command class but forgetting `context.RegisterConsoleCommand<TutorialPingCommand>()`
- Copying only the DLL instead of the full packaged plugin folder
- Expecting the plugin to hot reload without a server restart
- Setting the command source or account type differently from the way you plan to test it

## Next Step

Once this works, read:

- [Plugin System](plugins.md)
- [Console Commands](../operations/console-commands.md)
