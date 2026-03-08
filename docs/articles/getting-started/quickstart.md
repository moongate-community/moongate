# Quick Start Guide

Get Moongate v2 running locally with the current server behavior.

## Prerequisites

- .NET SDK 10.0.x
- Ultima Online client data directory
- Git

## 1. Clone

```bash
git clone https://github.com/moongate-community/moongatev2.git
cd moongatev2
```

## 2. Build

```bash
dotnet restore
dotnet build -c Release
```

## 3. Run

You must provide UO directory (CLI or env var).

```bash
dotnet run --project src/Moongate.Server -- \
  --root-directory ./moongate \
  --uo-directory /path/to/uo
```

Or with env var:

```bash
export MOONGATE_UO_DIRECTORY=/path/to/uo
dotnet run --project src/Moongate.Server
```

## 4. Verify

HTTP checks:

- `http://localhost:8088/health` → `ok`
- `http://localhost:8088/metrics` → Prometheus payload (or config message)
- `http://localhost:8088/scalar` → OpenAPI UI (if enabled)

## 5. Console Commands

Built-in default commands currently include:

- `help` / `?` (console + in-game, minimum `Regular`)
- `lock` / `*` (console only, minimum `Administrator`)
- `exit` / `shutdown` (console only, minimum `Administrator`)
- `add_user` (console + in-game, minimum `Administrator`)
- `send_target` (in-game only, minimum `Regular`)
- `orion` (in-game only, minimum `Regular`, requests target cursor and spawns Orion on selected tile)
- `teleport` / `tp` (in-game only, minimum `GameMaster`, usage: `.teleport <mapId> <x> <y> <z>`)
- `add_item_backpack` / `.add_item_backpack` (in-game only, minimum `GameMaster`, usage: `.add_item_backpack <templateId>`)

Command source and authorization rules:

- Console commands are always treated as `AccountType.Administrator`.
- In-game commands use the authenticated `GameSession.AccountType`.
- In-game command input is triggered by Unicode speech starting with `.` (example: `.help`).

### Register a New C# Command

Built-in commands are now registered with `ICommandExecutor` + `[RegisterConsoleCommand]`.
The registration is source-generated at build time (no manual bootstrap wiring required).

```csharp
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

[RegisterConsoleCommand(
    "whoami|me",
    "Shows basic identity information.",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class WhoAmICommand : ICommandExecutor
{
    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        context.Print("You are connected.");
        return Task.CompletedTask;
    }
}
```

`ICommandSystemService.RegisterCommand(...)` is still available for dynamic/runtime scenarios (for example Lua-driven command registration).

## 6. Docker (optional)

Build image:

```bash
./scripts/build_image.sh -t moongate-server:local
```

Run:

```bash
docker run --rm -it \
  -p 2593:2593 \
  -p 8088:8088 \
  -v /path/to/moongate-data:/app \
  -v /path/to/uo:/uo:ro \
  --name moongate \
  moongate-server:local
```

## Troubleshooting

- If startup fails with UO directory error, set `--uo-directory` or `MOONGATE_UO_DIRECTORY`.
- If ports are busy, stop conflicting process or remap ports.

---

**Previous**: [Introduction](introduction.md) | **Next**: [Installation Guide](installation.md)
