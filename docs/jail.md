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
Days: [ 3 ]

[>] Target: Lord Pippo
Jail                              Release  Go
[>] Cell 1   free                          [>]
    Cell 2   Gino - 2d 4h left    [x]      [>]
[>] Cell 3   free                          [>]
    ...
```

1. Press `Target` and pick the character with the cursor: a player or an NPC. The gump opens
   again with its name. Until then the cells are only listed: no cell has a button to jail.
2. Type the days of the sentence.
3. Press the button of a free cell.

The character stands in that cell at once and is told `You have been jailed for 3 days.`; you
are told `Lord Pippo is in cell 3 for 3 days.`

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
Jailed by Giachi.
```

The fine on the note is the gold really taken. The text is written in the server's language
(message 30143 of [`messages`](data-files/messages.md)), with the dates in UTC. The note also
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
if jail.send(target, 2, 3, who) == JailResultType.Ok then
    mobile.message(who, "Done.")
end

for _, cell in ipairs(jail.cells()) do
    if cell.prisoner then
        log.info(cell.name .. " leaves cell " .. cell.number .. " in " .. cell.seconds_left .. " seconds")
    end
end
```

`jail.release(serial)` ends a sentence early and `jail.sentence(serial)` reads one. The module
does not check who calls it: a script for the staff checks `world.is_staff` first, as the gump
script [`jail_sentence.lua`](scripting/shipped-scripts.md#jail_sentencelua) does. The gump is
[`templates/gumps/jail_sentence.xml`](gumps.md); both are yours to change.

## What it does not do yet

- Forbid anything in jail: there are no spells, skills or recall to forbid.
- Jail a player who is offline.
- Keep the reason of an arrest or a record of past sentences.
- Jail a whole account: a sentence is of one character.

## See also

- [`jail`](commands/jail.md): the command.
- [`jail.toml`](data-files/jail.md): the cells.
- [Gumps](gumps.md) and [NPC spawns](spawns.md).
