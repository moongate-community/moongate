# Server commands

Moongate accepts commands through its interactive server console. Press `*` to
unlock the prompt after startup; [`console lock`](commands/console.md) locks it again. On the
console, TAB completes the command name (a second TAB lists the commands that match what you
typed), Up and Down walk the lines you sent since the start (the last 100; an `account create`
line with a password is never kept), and Escape clears the line. In game, type a command with a leading dot,
such as `.help`. Commands are separated on whitespace; quoted arguments and
passwords containing spaces are not supported.

The command registry describes which commands may run from `InGame` and their
minimum account level. The console is treated as an administrator; in-game
commands use the invoking session's account level. Command input and ordinary
command output stay private to the caller. `broadcast` explicitly sends a system
message to everyone in the local world, and a successful `save` announces completion.
`shutdown` also announces the requested server stop to players.
Use `..text` to say `.text` literally.

## All commands at a glance

"Console" means the server console (always treated as an administrator); "In game" means
typed with a leading dot by a character whose account has at least the minimum level. The
role is the server mode that registers the command: `Login`, `Game`, or both in `Standalone`.

| Command | Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- | --- |
| [`help`](commands/help.md) | `help [command]` | Yes | Yes | Regular | Every role |
| [`echo`, `e`](commands/echo.md) | `echo <text>` | Yes | Yes | Regular | Every role |
| [`time`](commands/time.md) | `time` | No | Yes | Regular | Game |
| [`console`](commands/console.md) | `console lock` | Yes | No | — | Every role |
| [`script`](commands/script.md) | `script reload <file>` / `script metrics` | Yes | No | — | Game |
| [`account`](commands/account.md) | `account create <username> <password> [level]` / `account api-access <username> <on\|off>` | Yes | Yes | Administrator | Login |
| [`character`](commands/character.md) | `character pending [account-serial]` / `character restore <character-serial>` | Yes | Yes | GameMaster | Game |
| [`save`](commands/save.md) | `save` | Yes | Yes | Administrator | Game |
| [`sql_backup`](commands/sql_backup.md) | `sql_backup` | Yes | Yes | Administrator | Every role |
| [`broadcast`](commands/broadcast.md) | `broadcast <text>` | Yes | Yes | Administrator | Game |
| [`shutdown`](commands/shutdown.md) | `shutdown [seconds]` | Yes | Yes | Administrator | Game |
| [`decorate`](commands/decorate.md) | `decorate` | Yes | Yes | Administrator | Game |
| [`initial_spawn`](commands/initial_spawn.md) | `initial_spawn` | Yes | Yes | Administrator | Game |
| [`globallight`](commands/globallight.md) | `globallight [0-31]` | Yes | Yes | GameMaster | Game |
| [`spawn`](commands/spawn.md) | `spawn <template>`, then target a spot | No | Yes | GameMaster | Game |
| [`add`](commands/add.md) | `add <template>`, then target a spot | No | Yes | GameMaster | Game |
| [`remove`](commands/remove.md) | `remove`, then target an NPC or an item on the ground | No | Yes | GameMaster | Game |
| [`where`](commands/where.md) | `where`, then target anything | No | Yes | GameMaster | Game |
| [`go`](commands/go.md) | `go [<x>,<y>,<z> [map] \| <place>]` | No | Yes | GameMaster | Game |
| [`moongate`](commands/moongate.md) | `moongate <x>,<y>,<z> [map]` | No | Yes | GameMaster | Game |
| [`fame`](commands/fame.md) | `fame <0..32000>`, then target a mobile | No | Yes | GameMaster | Game |
| [`karma`](commands/karma.md) | `karma <-32000..32000>`, then target a mobile | No | Yes | GameMaster | Game |
| [`weather`](commands/weather.md) | `weather [none\|rain\|snow\|storm]` | No | Yes | GameMaster | Game |
| [`season`](commands/season.md) | `season [spring\|summer\|fall\|winter\|desolation\|auto]` | No | Yes | GameMaster | Game |
| [`spawns`](commands/spawns.md) | `spawns` | No | Yes | GameMaster | Game |
| [`gump`](commands/gump.md) | `gump <id> [name=value ...]` | No | Yes | GameMaster | Game |
| [`music`](commands/music.md) | `music [track]` | No | Yes | GameMaster | Game |
| [`lock`](commands/lock.md) | `lock`, then target a door | No | Yes | GameMaster | Game |
| [`unlock`](commands/unlock.md) | `unlock`, then target a door | No | Yes | GameMaster | Game |
| [`key`](commands/key.md) | `key`, then target a door | No | Yes | GameMaster | Game |

### By who uses them

- **Everyone:** `help`, `echo`, `time`.
- **Game masters:** `character`, `spawn`, `add`, `remove`, `where`, `go`, `moongate`, `fame`, `karma`, `globallight`,
  `weather`, `music`, `season`, `spawns`, `gump`, `lock`, `unlock`, `key`.
- **Administrators:** `account`, `save`, `sql_backup`, `broadcast`, `shutdown`, `decorate`,
  `initial_spawn`, plus everything a
  game master uses.
- **Console only:** `console`, `script`.

Commands that ask for a target open the client's target cursor after checking their
arguments; pressing Escape prints `Target canceled.` and changes nothing. The texts they
print are in the server language (see [Localization](localization.md)).
