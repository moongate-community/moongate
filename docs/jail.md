# Jail

A game master sends a character to a jail cell for a number of days. When the days are over the
prisoner goes back where it was arrested, pays a fine in gold and finds a note in its backpack
that says what it served and what it paid. Every cell has a chest of bread and water. Players and
NPCs are jailed the same way.

The cell picked from a gump, the sentence in days, the fine, the note and the rations are
Moongate's own: ModernUO picks a cell at random for a time it decides itself, UOX3 takes a
number of seconds from a command, and neither takes a fine or feeds its prisoners.

## Send someone to jail

[`.jail`](commands/jail.md), for game masters and above, opens the gump of the jail:

```text
Jail
Days: [ 3 ]   Reason: [ Stole a horse          ]

[>] Target: Lord Pippo
Jail                              Release  Go
[>] Cell 1   free                          [>]
    Cell 2   Gino - 2d 4h left    [x]      [>]
[>] Cell 3   free                          [>]
    ...
```

1. Press `Target` and pick the character with the cursor: a player or an NPC. The gump opens
   again with its name. Until then the cells are only listed: no cell has a button to jail.
2. Type the days of the sentence and, if you want, its reason: up to 60 characters in the gump.
   Picking a character already in jail fills the field with its reason, so moving it to another
   cell keeps it.
3. Press the button of a free cell.

The character stands in that cell at once and is told `You have been jailed for 3 days: Stole a
horse`, or `You have been jailed for 3 days.` with no reason; you are told `Lord Pippo is in cell 3
for 3 days.` The reason is kept with the sentence as one line of plain text, written in the
[console](#in-the-console-and-the-log) and on the [release note](#the-release-note): line breaks
become spaces, `<` and `>` are taken out, and what goes beyond 100 characters is cut. The gump
field takes 60; the 100 are the limit for a script that calls `jail.send`.

- A cell holds one prisoner. A cell that holds someone shows its name and the time left, and has
  no button to jail.
- The days are a whole number from 1 to [`ultima.jail.max_days`](#settings). Anything else
  jails nobody and opens the gump again.
- You cannot jail yourself, nor a player whose account has your rank or a higher one.
- Target a character that is already in jail and the gump has its release at the top; the button
  of a free cell moves it there, with a sentence that starts now. It still goes back where it
  was first arrested.
- `Target` again picks someone else; a cursor put away with Escape keeps the character the gump
  had. A cursor that another one replaces, such as that of a second `.jail`, opens nothing.
- A right click closes the gump.

## Jail a player who is offline

`.jail Pippo` looks for the player of that name and opens the gump on it, whether it is in the
world or not. The name is typed whole, in any case.

```text
[>] Target: Pippo (offline)
Jail                              Release  Go
[>] Cell 1   free                          [>]
    Cell 2   Gino - waits for login  [x]   [>]
```

Type the days and the reason and press the button of a free cell, as for anyone else. A player
who is online stands in the cell at once. One who is offline is not moved, and you are told
`Pippo will be in cell 3 for 3 days from its next login.`:

- The cell is kept for it: it shows `Pippo - waits for login` and nobody else can be sent there.
- Within ten seconds of its next login the player is taken to the cell and told its sentence.
  **Its days start then**, not on the day you gave the sentence: three days are three days in the
  cell.
- When the days are over it goes back where it logged in, with the fine and the note of any
  other prisoner.
- `Release` on a sentence that waits drops it: nobody was moved, so there is no fine and no note.
- A sentence that waits never expires. If the player does not come back, the cell is free again
  only with `Release`.
- Pressing another free cell for a player whose sentence waits moves the cell kept for it.
- The rank counts offline too: you cannot jail a character whose account has your rank or a
  higher one.

When several players have the name, the gump lists them instead of the cells:

```text
[>] Target: nobody. Pick one of these, or press the button.
[>] Pippo - account mario - online
[>] Pippo - account luigi - offline
```

Press the button of the one you mean: the gump opens on it, with the cells. Ten are listed at
most; with more than ten players of one name the gump says how many it left out, and those can
only be picked with `Target` while they are online.

A player who was jailed while online and then logged out can be moved the same way: `.jail
<name>`, then a free cell. Its sentence waits again and starts over at its next login, and it
still goes back where it was first arrested.

A player caught in the few seconds of its login is treated as one who is offline: the sentence
waits, and the next check takes it to the cell.

NPCs are not found by name: jail them with `Target`.

## Visit the cells

`Go`, on every cell of the gump, takes you into it, on the map of the jail, and leaves the gump
open; it needs no target. It is the way to see a prisoner: the places `Cell 1` to `Cell 10` of
[`locations.toml`](data-files/locations.md) exist on Felucca and on Trammel, and
[`.go cell 1`](commands/go.md) takes the one of the map you stand on, an empty room everywhere but
on the jail's map. The cells are closed rooms: leave with `.go` or with another `Go`.

## The sentence

The days are real days. They run while the prisoner is offline and while the server is down:
a sentence of 3 days given on Monday at noon ends on Thursday at noon.

The server looks every ten seconds for the sentences that are over.

- A prisoner in the world is released at once.
- A player who is offline is released within ten seconds of its next login. Its cell is free
  from the moment the sentence is over.
- A sentence given to a player who was offline has not started: it
  [waits for its login](#jail-a-player-who-is-offline) and is never over before.
- The sentence of an NPC that was removed meanwhile is dropped.
- A prisoner who holds an item on its cursor waits in its cell until it drops it: gold on the
  cursor cannot be taken, and lifting it is no way around the fine.

Sentences are kept in the table `world.jail_sentences` and written by the world save, so a
restart forgets none.

Nothing is forbidden in jail yet: spells, skills and travel do not exist. The cells are closed
rooms with no door, and the region is [dim](server-configuration.md).

## The release

When a sentence ends:

1. The fine is taken: up to [`ultima.jail.fine_gold`](#settings) coins, from the gold piles in
   the backpack and the bags inside it, then from the bank box. When the prisoner has less, what
   it has is taken and it leaves anyway. An NPC with no gold pays nothing.
2. The prisoner goes back where it was arrested. When that map is no longer loaded it goes to the
   `release` spot of [`jail.toml`](data-files/jail.md).
3. A release note goes in its backpack.
4. A player is told `You have served your sentence. A fine of 500 gold was taken.`

A sentence is ended before its release runs, so nobody pays twice. When a release fails halfway,
such as a teleport onto a spot that is gone, the log has `The release of <serial> from jail
failed` and the prisoner may still stand in its cell with no sentence: move it out by hand with
[`.go`](commands/go.md) or a teleport. A note that could not be made is in the log too.

`Release` in the gump ends a sentence early: no fine, no note. A prisoner in the world goes back
at once, a player who is offline at its next login.

### The release note

The note is the item `jail_release_note`. A double click shows what the jail wrote on it:

```text
Lord Pippo served 3 days in cell 2, from 2026-10-04 to 2026-10-07, and paid a fine of 500 gold.
Jailed by Giachi. Reason: Stole a horse
```

The reason is there when one was given (message 30149).

The fine on the note is the gold really taken. The text is written in the server's language
from [`templates/books/jail_release_note.toml`](data-files/books.md), with the dates in UTC.
Its recipient name is the name recorded with the sentence, even after a rename. The rendered
`book.title`, `book.author` and `book.content` stay fixed when traded or read. Old `jail.text`
notes remain readable. The note also
keeps the props `jail.cell`, `jail.days` and `jail.fine` for scripts; its script is
[`jail_note.lua`](scripting/shipped-scripts.md#jail_notelua).

## The chest of rations

Every cell has a chest that cannot be moved, with 2 to 4 loaves of bread and a pitcher of water.
A prisoner eats and drinks from it, so a long sentence does not leave it
[starving and parched](server-configuration.md).

| File | What it holds |
| --- | --- |
| `templates/items/jail.toml` | The chest `jail_chest` and the note `jail_release_note` |
| `templates/loots/jail.toml` | The loot tables `jail_bread` and `jail_water`, one entry each, so a chest always has both |
| `templates/spawns/felucca/jail.toml` | One [region of items](spawns.md#regions-of-items-treasure-chests) per cell: a single tile in the north-east corner of the cell |

The chest decays after 60 minutes, eaten or not, and its region puts a full one back a minute or
two later. To feed the prisoners something else, change the two loot tables. An existing world
gets the chests at its next spawn check; [`.initial_spawn`](commands/initial_spawn.md) fills
them at once.

## In the console and the log

The server says who goes in and who comes out, at the information level:

```text
Lord Pippo (0x00000A12) is jailed in cell 3 for 3 days by Giachi: Stole a horse
Aria (0x00000A40) will be jailed in cell 4 for 2 days at its next login, by Giachi: Insulted the staff
The sentence of Aria (0x00000A40) for cell 4 is dropped before it began
Lord Pippo (0x00000A12) is released from cell 3 after 3 days, with a fine of 500 gold
Gino (0x00000B07) is released early from cell 2
The sentence of an orc (0x0000E258) in cell 1 is dropped: it is no longer in the world
```

A character moved to another cell is said to be jailed again, in the new cell. The fine is the
gold really taken. The last line is an NPC that was removed while it served. The second line is
a sentence given to a player who was offline; when it logs in the server writes the usual `is
jailed in cell` line, and the third line is that sentence released before the player came back.
A sentence that waits for a cell that is gone from `jail.toml`, or whose map is not loaded, writes
a warning at every check after the login, `waits for cell 4, which cannot be reached`, and keeps
waiting.

## Settings

```toml
[ultima.jail]
fine_gold = 500   # Coins taken when a sentence ends; 0 takes nothing.
max_days = 30     # The longest sentence the gump accepts.
```

`fine_gold` goes from 0 to 1,000,000,000 and `max_days` from 1 to 3650. The gold is the item
template of `ultima.items.gold_template`.

## The cells

The cells are in [`data/jail.toml`](data-files/jail.md): the ten of Felucca's jail, the same
ModernUO and UOX3 use. To add a cell, add its `[[cell]]` there and its chest region in
`templates/spawns/felucca/jail.toml`. Without the file the jail is off and `.jail` says so.

## For scripts

The [`jail` module](https://moongate.sh/lua/jail/) is what the gump uses:

```lua
if jail.send(target, 2, 3, who, "Stole a horse") == JailResultType.Ok then
    mobile.message(who, "Done.")
end

for _, cell in ipairs(jail.cells()) do
    if cell.prisoner then
        log.info(cell.name .. " leaves cell " .. cell.number .. " in " .. cell.seconds_left .. " seconds")
    end
end
```

`jail.release(serial)` ends a sentence early and `jail.sentence(serial)` reads one. A sentence
that waits for its player to log in has `pending` set, in `jail.sentence` and in the cell kept for
it, and `seconds_left` is then its whole length. `jail.send` answers `JailResultType.Pending` for a
player who is offline, and only for one that `.jail <name>` found: a script cannot jail any serial
that is not in the world. The module
does not check who calls it: a script for the staff checks `world.is_staff` first, as the gump
script [`jail_sentence.lua`](scripting/shipped-scripts.md#jail_sentencelua) does. The gump is
[`templates/gumps/jail_sentence.xml`](gumps.md); both are yours to change.

## What it does not do yet

- Forbid anything in jail: there are no spells, skills or recall to forbid.
- Keep a record of past sentences: the reason lives with the sentence and on its note.
- Jail a whole account: a sentence is of one character.

## See also

- [`jail`](commands/jail.md): the command.
- [`jail.toml`](data-files/jail.md): the cells.
- [Gumps](gumps.md) and [NPC spawns](spawns.md).

When upgrading an existing root, `mgctl init` preserves its jail item and script files. Complete the [two required file merges](data-files/books.md#existing-roots) before starting the upgraded server.
