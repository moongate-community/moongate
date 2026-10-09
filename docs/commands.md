# Server commands

Moongate accepts commands through its interactive server console. Press `*` to
unlock the prompt after startup; [`console lock`](commands/console.md) locks it again. On the
console, TAB completes the command name and then the arguments that have fixed values, such as
`account create` and its levels, `script reload` and the `.lua` files, `help` and the command
names (a second TAB lists what matches what you typed); it never offers a user name or a
password. Up and Down walk the lines you sent since the start (the last 100; an `account create`
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
| [`version`](commands/version.md) | `version` | Yes | Yes | Regular | Every role |
| [`uptime`](commands/uptime.md) | `uptime` | Yes | Yes | Regular | Every role |
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
| [`set`](commands/set.md) | `set <hits\|mana\|stamina\|hunger\|thirst\|criminal> <value>`, then target a mobile | No | Yes | GameMaster | Game |
| [`add`](commands/add.md) | `add <template>`, then target a spot | No | Yes | GameMaster | Game |
| [`remove`](commands/remove.md) | `remove`, then target an NPC or an item on the ground | No | Yes | GameMaster | Game |
| [`kill`](commands/kill.md) | `kill`, then target an NPC | No | Yes | GameMaster | Game |
| [`tame`](commands/tame.md) | `tame [name]`, then target a creature | No | Yes | GameMaster | Game |
| [`resurrect`](commands/resurrect.md) | `resurrect`, then target a corpse | No | Yes | GameMaster | Game |
| [`animate`](commands/animate.md) | `animate <action>`, then target a mobile | No | Yes | GameMaster | Game |
| [`where`](commands/where.md) | `where`, then target anything | No | Yes | GameMaster | Game |
| [`go`](commands/go.md) | `go [<x>,<y>,<z> [map] \| <place>]` | No | Yes | GameMaster | Game |
| [`gmtools`](commands/gmtools.md) | `gmtools` | No | Yes | GameMaster | Game |
| [`pages`](commands/pages.md) | `pages` | No | Yes | GameMaster | Game |
| [`event`](commands/event.md) | `event [list\|on\|off\|auto <id>]` | Yes | Yes | Administrator | Game |
| [`hide`](commands/hide.md) | `hide` | No | Yes | GameMaster | Game |
| [`unhide`](commands/unhide.md) | `unhide` | No | Yes | GameMaster | Game |
| [`moongate`](commands/moongate.md) | `moongate <x>,<y>,<z> [map]` | No | Yes | GameMaster | Game |
| [`jail`](commands/jail.md) | `jail [name]` | No | Yes | GameMaster | Game |
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
| [`book`](commands/book.md) | `book <template> [name=value ...]` | No | Yes | GameMaster | Game |
| [`create_check`](commands/create_check.md) | `create_check <1..2000000000>` | No | Yes | GameMaster | Game |
| [`add_gold`](commands/add_gold.md) | `add_gold <1..60000>`, then target a mobile | No | Yes | GameMaster | Game |

### From a script

A script runs any of these commands with the `commands` module of Lua:

```lua
commands.execute("season", "winter")          -- as the console: every power, no player
commands.execute_as(player, "go", "britain")  -- as that player wrote it in game
```

- `commands.execute(name, ...)` runs the command as the server console does. A command that needs a
  player, such as one that opens a target cursor, answers that it works in game only. What the
  command answers is written in the server log.
- `commands.execute_as(player, name, ...)` runs it as that player: with the level of its account,
  so a command above it is refused, and with its session, so a cursor opens for it. The player
  reads what the command answers.
- The arguments follow the name, one each: strings, numbers and booleans. They are joined by
  spaces into one line, so an argument with a space in it is read as two.
- Both answer `true` when the command was started and `false` when there is nothing to run: an
  empty name, a line end in the name or in an argument, a player that is not in the world. The
  command runs on its own, as one typed in game does: the script does not wait for it and does
  not get what it answered.

### By who uses them

- **Everyone:** `help`, `echo`, `time`.
- **Game masters:** `character`, `spawn`, `add`, `set`, `remove`, `kill`, `tame`, `resurrect`, `animate`, `where`, `go`, `gmtools`, `pages`, `hide`, `unhide`, `moongate`, `fame`, `karma`, `globallight`,
  `weather`, `music`, `season`, `spawns`, `gump`, `lock`, `unlock`, `key`, `book`, `create_check`, `add_gold`.
- **Administrators:** `account`, `save`, `sql_backup`, `broadcast`, `shutdown`, `event`, `decorate`,
  `initial_spawn`, plus everything a
  game master uses.
- **Console only:** `console`, `script`.

Commands that ask for a target open the client's target cursor after checking their
arguments; pressing Escape prints `Target canceled.` and changes nothing. The texts they
print are in the server language (see [Localization](localization.md)).
