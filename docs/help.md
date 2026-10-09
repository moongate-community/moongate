# Help

The Help button of the paperdoll opens a small menu. It does not wait for a game master: the three
buttons answer the player at once. A queue of requests for the game masters is the next step, see
[Not yet](#not-yet).

```text
Help
[>] I am stuck
[>] Useful commands
[>] Server rules
```

A right click closes the menu. A ghost can open it too, which is when it is needed most. The packet
is 0x9B (see [Packets](packets.md)); the menu is the gump `help_menu`
(`templates/gumps/help_menu.xml`, script `scripts/gumps/help_menu.lua`).

## I am stuck

The button takes a character that cannot get out of a trap, a house or a cave to the nearest starting
city.

1. The character is told `Stand still for 5 seconds and you will be taken to Britain.`
2. If it does not move for the wait, it appears in that city and is told `You have been taken to
   Britain.` If it moves, nothing happens: `You moved: you stay where you are.`
3. The city is the one of [`data/starting_cities.toml`](data-files/starting-cities.md) nearest to the
   character on the map it stands on. When no city is on that map, it is the first of the file.

The button refuses, with a text, when:

- the character is in [jail](jail.md);
- the character is fighting;
- it already asked and the wait is not over: `You already asked to be moved: stand still.`;
- it asked less than the pause ago: `You can ask to be moved again in 7 minutes.` The pause is kept
  with the character, so a relog does not reset it, and a request that moved nobody does not spend it.

Jail and fighting are checked again when the wait is over. A character that logs out during the wait
is not moved. Game masters and above have no pause.

## Useful commands

Runs [`.help`](commands/help.md) as the player: the commands its account may run.

## Server rules

Tells the rules of the server in one system message, message 30200. Change the text in
`data/messages/<language>/moongate.toml` (see [Localization](localization.md)).

## Settings

```toml
[ultima.help]
stuck_wait_seconds = 5        # The seconds a character must stand still before "I am stuck" moves it.
stuck_cooldown_minutes = 10   # The minutes before a player can use "I am stuck" again; 0 allows it at once.
```

`stuck_wait_seconds` goes from 1 to 60 and `stuck_cooldown_minutes` from 0 to 1440. A value out of
range stops the server at startup and the error names the setting.

## For scripts

The Lua module `help` gives a script what the menu uses:

| Function | What it gives |
| --- | --- |
| `help.settings()` | `{ wait_seconds, cooldown_minutes }` |
| `help.nearest_city(player)` | `{ town, x, y, z, map }` of the nearest starting city, or nil |

The texts of the menu are the messages 30192 to 30200, in every shipped language.

## Not yet

- Call a game master: a request with a kind and a text, kept in a queue the staff works through, with
  an answer to the player.
- A menu entry for harassment reports.
