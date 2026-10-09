# Help

The Help button of the paperdoll opens a small menu. Three of its buttons answer the player at once;
the fourth sends a request to the game masters, who work through a [queue](#the-queue-of-the-staff).

```text
Help
[>] I am stuck
[>] Useful commands
[>] Server rules
[>] Call a game master
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
is not moved. When the map of the city is not loaded the character stays where it is, is told `There is no
city to take you to.` and spends no pause. Every move is logged at Information with the player, where it
was and the city. Game masters and above have no pause.

## Useful commands

Runs [`.help`](commands/help.md) as the player: the commands its account may run.

## Server rules

Tells the rules of the server in one system message, message 30200. Change the text in
`data/messages/<language>/moongate.toml` (see [Localization](localization.md)).

## Call a game master

The button opens a second gump, `help_page_kind`, that asks what the request is about:

```text
What is it about?
[>] Question
[>] Bug
[>] Suggestion
[>] Harassment
```

1. The player picks a kind and is told `Type what you need in the journal line.`
2. It types one line, up to 128 characters; Escape or an empty line sends nothing: `Nothing was sent.`
3. The request goes to the queue and the player is told `Your request was sent to the game masters.`
   Every game master and administrator in the world reads `Gino asks for help (Bug): The door is stuck.`

A player has one request at a time: while one is open or taken it reads `You already asked for help: wait
for an answer.` and, after it was closed, it waits [`page_cooldown_seconds`](#settings) before the next:
`Wait 60 seconds before asking again.` Both are told before the line is typed, and again after it when
something changed meanwhile. A player in [jail](jail.md) can call a game master too.

## The queue of the staff

[`.pages`](commands/pages.md), for game masters and above, opens the queue: the open and taken
requests, the oldest first, ten a page.

```text
Help requests
[>] #1 Gino, Bug, 3 min, open
[>] #2 Pina, Harassment, now, taken by Gino
```

A row opens the request: who asked, what about, where, how long ago, and the text of the player.

- **Go to the player** takes the game master to where the player stands now, or to where it asked when it
  is offline.
- **Take** marks the request as taken by this game master; another one can take it over.
- **Send the answer** sends the line typed in the field, up to 128 characters, to the player and closes
  the request.
- **Close without an answer** closes it with nothing to deliver.
- **Back** returns to the queue.

A request that another game master closed meanwhile says `Request 1 is already closed.` and nothing
changes. Game masters and administrators that enter the world are told how many requests wait: `2 help
requests are waiting. Type .pages.`

## The answer

The player reads `Game master Gino answers: Go north.` at once when it is in the world. When it is
offline the answer waits, is kept across restarts of the server, and is told once at its next login.
The requests live in the table `world.help_pages` and are written by the world save. A closed request
is kept for [`page_history_days`](#settings) and the startup deletes it afterwards.

## Settings

```toml
[ultima.help]
stuck_wait_seconds = 5        # The seconds a character must stand still before "I am stuck" moves it.
stuck_cooldown_minutes = 10   # The minutes before a player can use "I am stuck" again; 0 allows it at once.
page_cooldown_seconds = 60    # The seconds between two requests of one player to the game masters; 0 allows it at once.
page_history_days = 30        # The days a closed request is kept before it is deleted at startup.
```

`stuck_wait_seconds` goes from 1 to 60, `stuck_cooldown_minutes` from 0 to 1440, `page_cooldown_seconds`
from 0 to 3600 and `page_history_days` from 1 to 3650. A value out of range stops the server at startup
and the error names the setting.

## For scripts

The Lua module `help` gives a script what the menu uses:

| Function | What it gives |
| --- | --- |
| `help.settings()` | `{ wait_seconds, cooldown_minutes }` |
| `help.nearest_city(player)` | `{ town, x, y, z, map }` of the nearest starting city, or nil |
| `help.can_page(player)` | `{ ok }`, or `{ ok = false, reason, seconds }` with `reason` `open`, `wait` or `gone` |
| `help.create_page(player, kind, text)` | `{ id }`, or `{ reason, seconds }` with `reason` `open`, `wait`, `text` or `gone`; `kind` is a `HelpPageKindType` |
| `help.pages()` | the open and taken requests, oldest first, each `{ id, player, name, account, kind, status, text, taken_by, answer, age_seconds, map, x, y, z, online }` |
| `help.page(id)` | one request in that shape, closed ones included, or nil |
| `help.take(id, staff)`, `help.answer(id, staff, text)`, `help.close(id, staff)` | true when the request was open or taken; the name of the game master is the one of the `staff` mobile |
| `help.waiting()` | how many requests are open or taken |

The enums `HelpPageKindType` (`Question`, `Bug`, `Suggestion`, `Harassment`) and `HelpPageStatusType`
(`Open`, `Taken`, `Closed`) are published to Lua. The module does not check who calls it: a script for the
staff checks `world.is_staff` first.

The texts the players and the staff read are the messages 30192 to 30220, in every shipped language.
The labels and messages of the staff gumps are in English, like the other staff gumps.

## Not yet

- A conversation between the player and the game master, or more than one answer to a request.
- Actions against the player reported for harassment.
