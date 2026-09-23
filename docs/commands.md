# Server commands

Moongate accepts commands through its interactive server console. Press `*` to
unlock the prompt after startup. Commands are separated on whitespace; quoted
arguments and passwords containing spaces are not supported.

The command registry also describes which commands may run from `InGame` and their
minimum account level. No packet handler currently submits in-game commands, so
the console is the available input path. The console is treated as an administrator;
in-game access, when added, will use the invoking session's account level.

| Command | Console | In-game registration | Minimum in-game level | Purpose |
| --- | --- | --- | --- | --- |
| `echo`, `e` | Yes | Yes | Regular | Print the arguments back to the caller |
| `help` | Yes | Yes | Regular | List accessible commands or show details for one command |
| `script` | Game/Standalone | No | — | Reload one Lua script or show script metrics |
| `account` | Login/Standalone | Yes | Administrator | Create an account in the Accounts database |

## Help

```text
help
help account
help e
```

`help` lists each command available to the caller once, under its primary name.
`help <name>` also accepts an alias and shows the description, aliases, allowed
sources, and minimum account level. In-game callers only see commands allowed for
their source and account level.

## Echo

```text
echo hello world
e hello world
```

Both forms print `hello world`. With no arguments, the command prints a blank line.

## Script

```text
script reload ai/guard.lua
script metrics
```

`script reload` reloads one file relative to the configured `scripts/` directory.
It runs the reload on the game loop and reports an error if the script fails to
load. `script metrics` prints the Lua engine's current counters.

## Account

```text
account create <username> <password> [Regular|GameMaster|Administrator]
```

The account level defaults to `Regular`. The command waits for
`IAccountService.CreateAccountAsync` and reports success, an existing username, or
an error without printing the password. The interactive console masks the password
token while it is typed and does not include the raw command line in its error log.
The account is stored in the shared Accounts PostgreSQL database.
Game-only processes do not register this command or receive Accounts credentials.

The command is registered for in-game administrators, but there is no in-game
command input yet. Before enabling one, its input path must protect the password
from display and logs.

Plugins can add commands through `RegisterCommand<TExecutor>`; see
[Writing a plugin](plugins.md#console-commands).
