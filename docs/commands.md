# Server commands

Moongate accepts commands through its interactive server console. Press `*` to
unlock the prompt after startup. In game, type a command with a leading dot,
such as `.help`. Commands are separated on whitespace; quoted arguments and
passwords containing spaces are not supported.

The command registry describes which commands may run from `InGame` and their
minimum account level. The console is treated as an administrator; in-game
commands use the invoking session's account level. The command and its output
are never broadcast to nearby players. Use `..text` to say `.text` literally.

| Command | Console | In-game registration | Minimum in-game level | Purpose |
| --- | --- | --- | --- | --- |
| `echo`, `e` | Yes | Yes | Regular | Print the arguments back to the caller |
| `help` | Yes | Yes | Regular | List accessible commands or show details for one command |
| `script` | Game/Standalone | No | — | Reload one Lua script or show script metrics |
| `account` | Login/Standalone | Yes | Administrator | Create an account in the Accounts database |
| `character` | Game/Standalone | Yes | GameMaster | List characters pending deletion and restore them |

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
`characters.max_per_account`, so the player can create a new character in its
place. It stays restorable until it is removed; after
`characters.deletion_delay_hours` (default 24) it becomes eligible for removal,
by a job that is not built yet. `character pending` lists every
pending character, or those of one account, with when the deletion was requested
and when the character becomes eligible for removal. `character restore` cancels
the deletion and gives the character the first free slot; if the account filled
up meanwhile it stays without a slot, may exceed the limit by one, and appears in
the list once a slot frees. Serials
are hexadecimal with `0x` (`0x0000002A`) or decimal.
