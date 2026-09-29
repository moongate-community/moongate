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
Each successful command broadcasts `world saved in <elapsed>` to connected characters
currently in the world on this instance, across all maps. The console also prints
the same completion message; an in-game caller receives it through the broadcast.
The elapsed time measures the wait for saving, excluding broadcast delivery, and
uses the .NET `TimeSpan` format (for example, `world saved in 00:00:01.2345678`).

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
