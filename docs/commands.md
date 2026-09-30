# Server commands

Moongate accepts commands through its interactive server console. Press `*` to
unlock the prompt after startup. In game, type a command with a leading dot,
such as `.help`. Commands are separated on whitespace; quoted arguments and
passwords containing spaces are not supported.

The command registry describes which commands may run from `InGame` and their
minimum account level. The console is treated as an administrator; in-game
commands use the invoking session's account level. Command input and ordinary
command output stay private to the caller. `broadcast` explicitly sends a system
message to everyone in the local world, and a successful `save` announces completion.
`shutdown` also announces the requested server stop to players.
Use `..text` to say `.text` literally.

| Command | Console | In-game registration | Minimum in-game level | Purpose |
| --- | --- | --- | --- | --- |
| `echo`, `e` | Yes | Yes | Regular | Print the arguments back to the caller |
| `help` | Yes | Yes | Regular | List accessible commands or show details for one command |
| `script` | Game/Standalone | No | — | Reload one Lua script or show script metrics |
| `account` | Login/Standalone | Yes | Administrator | Create an account in the Accounts database |
| `character` | Game/Standalone | Yes | GameMaster | List characters pending deletion and restore them |
| `save` | Game/Standalone | Yes | Administrator | Save the world and announce completion |
| `broadcast` | Game/Standalone | Yes | Administrator | Send a system message to players on this instance |
| `shutdown` | Game/Standalone | Yes | Administrator | Stop the server gracefully, immediately or after a delay |
| `fame`, `karma` | No | Game/Standalone | GameMaster | Set the fame or karma of the character or NPC you target |
| `decorate` | Game/Standalone | Yes | Administrator | Place the world decoration: doors, signs, lights and furniture |
| `globallight` | Game/Standalone | Yes | GameMaster | Give every player the same light, or go back to the time of day |

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

In-game administrators can type `.account create ...`. The server does not echo
or broadcast the input and does not write it to its logs. The UO client may
retain the typed command in its own local history.

Plugins can add commands through `RegisterCommand<TExecutor>`; see
[Writing a plugin](plugins.md#console-commands).

Every text a command shows to players, its description in `help` and the dispatcher's replies
(unknown command, not available here, not allowed, failed) come from the message files
in the server language (`ILocalizationService`, ids 30008–30049; see
[Localization](localization.md#moongates-own-messages)). Command syntax, account
types, sources and map names stay technical names, as the commands take them. On a
login-only process, which has no message files, the texts are English. Operator-only
console output (`account api-access`, `script`) stays English.

### Local API access provisioning

```text
account api-access <username> <on|off>
```

Available only in the Login/Standalone local console, even when the caller is an in-game Administrator. Accounts created with `account create` start with API access disabled. Enable an existing Administrator to provision the first panel user; disabling access revokes its administrative sessions across hosts. Game login is unaffected. See [Administration API](admin-api.md).

## Character

```text
character pending [account-serial]
character restore <character-serial>
```

A character a player deletes from the character list is only marked for deletion:
it disappears from the list, gives up its slot and no longer counts toward
`ultima.characters.max_per_account`, so the player can create a new character in its
place. It stays restorable until it is removed; after
`ultima.characters.deletion_delay_hours` (default 24) it becomes eligible for removal,
by a job that is not built yet. `character pending` lists every
pending character, or those of one account, with when the deletion was requested
and when the character becomes eligible for removal. `character restore` cancels
the deletion and gives the character the first free slot; if the account filled
up meanwhile it stays without a slot, may exceed the limit by one, and appears in
the list once a slot frees. Serials
are hexadecimal with `0x` (`0x0000002A`) or decimal.

## Save

```text
save
```

In game, administrators use `.save`. The command requests a save through the existing
world save coordinator and waits for durable persistence to finish. A request made
during another save joins that save rather than starting a competing operation.
Each successful command broadcasts `The world has been saved in <seconds> seconds.`
(message 30015, in the server language) to connected characters currently in the
world on this instance, across all maps. The console also prints the same completion
message; an in-game caller receives it through the broadcast. The elapsed time
measures the wait for saving, excluding broadcast delivery, in seconds with two
decimals (for example, `The world has been saved in 1.23 seconds.`).

A failed save produces an error for the caller and no success broadcast. Extra
arguments print usage without saving. Automatic and shutdown saves keep their
existing behavior; this announcement belongs to the `save` command.

## Broadcast

```text
broadcast Server maintenance in five minutes.
```

In game, administrators use `.broadcast Server maintenance in five minutes.`.
The text is delivered as a Unicode system chat message to connected characters
currently in the world on this instance, regardless of map or distance. Character
selection sessions and disconnected clients are excluded. Other server instances
do not receive the message.

Text after the command name is sent without needing quotes. Leading and trailing
whitespace is trimmed; spaces inside the message are preserved. Empty input prints
usage and sends nothing. The caller receives the number of players whose outgoing
queues accepted the message; this is not a client receipt acknowledgment. In-game
input retains the existing 128-character speech limit, including the dot and command.
Console messages must fit both the Unicode speech packet and the compressed transport
limit. Oversized messages are rejected before any player receives them.

## Shutdown

```text
shutdown
shutdown 60
```

In-game administrators use `.shutdown` or `.shutdown 60`. With no argument or `0`,
the server announces `The server is shutting down now.` (message 30016) and requests
graceful shutdown. A positive number announces `The server will shut down in <seconds>
seconds.` (30017) and
schedules the stop. The command returns without waiting for the countdown; console
input and gameplay remain available until the deadline. The delay starts after
the announcement is queued and is rounded up to the server timer resolution.

The server accepts one shutdown request. Further requests report an error without
changing the deadline or repeating the announcement. Seconds must be a whole number
from `0` to `2147483647`; negative, fractional, overflowing and extra arguments are
rejected. A scheduled shutdown survives the invoking player disconnecting.

The command stops this process, including both roles in Standalone mode. It uses the
same ordered cleanup as the host shutdown path: services stop, the final world save
completes, and persistence is disposed. It does not force-kill the process. Other
instances are unaffected. A manual host stop during the delay takes precedence and
the timer is discarded with the game loop. There is no cancel or restart subcommand.

## Fame and karma

```text
.fame <0..32000>
.karma <-32000..32000>
```

In game only. The value is checked first; then a target cursor opens and the character or NPC
you pick gets it: `Bran now has 10000 fame.` Picking an item or cancelling changes nothing. The
paperdoll title follows at once: the [fame and karma prefix](data-files/titles.md), which says
`Lord` or `Lady` from 10,000 fame. The value is saved with the mobile by its next save.

## Decorate

```text
decorate
```

In game, administrators use `.decorate`. It places the
[decoration files](templates.md#decorations) of `templates/decorations/`, file by file, as
fixed items that never decay; the next world save keeps them. Doors and gates get the
`decoration_door` template, whose [door script](scripting.md) opens and closes them, and
adjacent doors of the same kind open together. Teleporters, spawners, mark containers, public
moongates and addons are skipped for now: they need their own logic.

Each file is reported when it is done, in game as a system message and in the server log:

```text
Decorating britannia/britain: 1180 placed, 3 already there, 12 skipped (Teleporter 8, Spawner 4).
```

The console and the in-game caller then get the totals:
`Decoration done: <placed> placed, <present> already there, <skipped> skipped in <files> files.`
An item with the same graphic already on the spot is kept, so running `decorate` again only
places what is missing. The files are read at each run: an edited file needs no restart. A
failure, such as a folder that is not a map, prints `The decoration failed. Check the server
logs.` and the reason goes to the log; the files done before it stay placed.

## Global light

```text
globallight 26
globallight
```

In game, game masters use `.globallight 26`. With a level from 0 (brightest) to 31 (darkest),
every player in the world gets that light at once: `The global light is now 26.` Without a
level, the light follows the time of day again (see
[the light cycle](server-configuration.md)). The override is not saved: a restart goes back to
the time of day.
