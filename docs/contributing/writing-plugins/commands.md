# Commands

A **command** is an action a staff member triggers by typing its name after the `.` prefix —
`.uptime`, `.broadcast`. A plugin can register its own, and the same command can be reachable
both in-game and from the [admin console](../../under-the-hood/admin-console.md).

## The interface

```csharp
public interface ICommand
{
    void Execute(CommandContext context);
}
```

The `CommandContext` carries the call: `context.Arguments` holds the tokens typed after the
command name, and `context.Reply(string)` sends a line back to whoever ran it.

```csharp
using System.Diagnostics;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Commands;

namespace MyShard.Ops.Commands;

/// <summary>Reports how long the server process has been running.</summary>
public sealed class UptimeCommand : ICommand
{
    public void Execute(CommandContext context)
    {
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

        context.Reply($"Uptime: {uptime:d\\.hh\\:mm\\:ss}");
    }
}
```

## Registering

Register the command in your plugin's `Configure`:

```csharp
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Abstractions.Types;

container.RegisterCommand<UptimeCommand>(
    "uptime|up",
    AccountLevelType.GrandMaster,
    "Reports server uptime.",
    CommandSourceType.InGame | CommandSourceType.Console
);
```

The four arguments:

- **name** — the command name, pipe-delimited for aliases (`"uptime|up"`); the first token is
  the canonical name, the rest are aliases.
- **minLevel** — the minimum `AccountLevelType` (`Moongate.Core.Types`) allowed to run it. The
  dispatcher refuses the command for anyone below it.
- **description** — the help text shown to tooling and command listings.
- **sources** — where the command can be invoked. `CommandSourceType` is a flags enum:
  `InGame | Console` exposes it both to `.uptime` in the game client and to the same command in
  the admin console. Omit `Console` to keep it in-game only.

A command is resolved from the container, so it can take dependencies through its constructor
like any other service — inject a game service and call it from `Execute`.
