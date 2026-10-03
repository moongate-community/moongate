# decorate

Places the world decoration: doors, signs, lights and furniture.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `decorate` | Yes | Yes | Administrator | Game |

```text
decorate
```

In game, administrators use `.decorate`. It first asks for confirmation with the
[gump](../gumps.md) `templates/gumps/decorate_confirm.xml`: CONTINUE goes on, CANCEL or closing
it prints `Decoration canceled.` and places nothing (without that file it does not ask). It places the
[decoration files](../templates.md#decorations) of `templates/decorations/`, file by file, as
fixed items that never decay; the next world save keeps them. Doors and gates get the
`decoration_door` template, whose [door script](../scripting.md) opens and closes them, and
two doors of the same kind side by side, at the same height and hung on opposite sides, open
together. Lights get the `decoration_light` template,
lit or unlit as in the data and protected, so only staff light or douse them with the
[light script](../scripting.md). Teleporters get the `decoration_teleporter` template: a player
who walks onto one stands on its destination at once ([teleporter script](../scripting.md)), and
only staff sees them. Besides those of the town and dungeon files, each map folder has the
teleporters of ModernUO's `[TelGen` (`teleporters.toml`, 1,478 in all); a cell keeps one teleporter
within 12 of height, the first placed, so the 230 that a town or dungeon file already lists are
reported as already there. Seven of Ter Mur lie outside its map in ModernUO's data and are skipped. A teleporter to another map takes the player there when that map is loaded, and does nothing otherwise. One whose `map_dest` names no map is skipped. A
`KeywordTeleporter`, such as the mantra of a shrine, gets the `decoration_keyword_teleporter`
template: a player who says its word within its range stands on its destination
([keyword teleporter script](../scripting.md)). The public moongates are not in the decoration
files: after them, `.decorate` places a gate with the `decoration_public_moongate` template on
every destination of [`moongates.toml`](../data-files/moongates.md) whose map is loaded, reported
as `<map>/moongates`. The crates, boxes, chests, barrels and bookcases of the towns (ModernUO's
`Fillable...` kinds and `LibraryBookcase`, 2,803 spots in the files, about 5,000 items with those of both Trammel and Felucca) take the `decoration_fillable`
template and fill up when a player opens them ([fillable script](../scripting.md#item-scripts));
those a run before this placed as plain decoration are turned into it and counted as already there. Every public moongate glows (prop `light = "circle300"`, as ModernUO). Spawners, mark
containers, addons and every other kind of teleporter (those that ask for a
skill, belong to a quest or want a double click) are skipped for now: they need their own logic.

The shop and world signs are decoration files too (`signs.toml` in the `britannia`, `felucca`,
`trammel`, `ilshenar`, `malas` and `tokuno` folders); a sign shows its
text when the mouse is on it. After the files come the doors of the towns, which no file lists:
as ModernUO's `[DoorGen`, the map's statics are read for door frames, and a dark wood door goes
between two frames two tiles apart, a linked double door when they are three apart. The two
frames must be within 1 of height of each other. A doorway that
is walled up or has no floor gets none, and neither does one where a door of a file already
stands: the items on the ground count like the statics, so a wall a decoration file placed closes
the doorway too. When one half of a double door does not fit, neither is placed. A few doorways
are left open on purpose, as in ModernUO. Trammel and Felucca are read inside the 16 rectangles
ModernUO scans, Ilshenar and Malas whole; they are read, when loaded, a small piece per game-loop
turn, so the game goes on meanwhile (about four seconds in all); each is reported as the file
`<map>/generated_doors`.

Each file is reported when it is done, in game as a system message:

```text
Decorating britannia/britain: 1180 placed, 3 already there, 5 skipped (SkillTeleporter 1, Spawner 4).
```

The server log has the same numbers, with the skipped kinds as a list of names and counts.

The console and the in-game caller then get the totals:
`Decoration done: <placed> placed, <present> already there, <skipped> skipped in <files> files.`
An item with the same graphic already on the spot is kept, and so is any door in a doorway, so
running `decorate` again only places what is missing. The files are read at each run: an edited file needs no restart. A
failure, such as a folder that is not a map, prints `The decoration failed. Check the server
logs.` and the reason goes to the log; the files done before it stay placed.

## See also

- [All commands](../commands.md)
- [`lock`](lock.md)
- [`key`](key.md)
