# go

Takes you to a place: one you pick from a list, one you name, or a spot you give in numbers.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `go [<x>,<y>,<z> [map] \| <place>]` | No | Yes | GameMaster | Game |

```text
.go
.go britain
.go covetous entrance
.go 1496,1628,10
.go 5690 569 25
.go 1496,1628,10 Felucca
```

In game only.

## Pick from the list

`.go` alone opens a gump with the named places of [`locations.toml`](../data-files/locations.md),
on the level of your map: its categories first (`Dungeons`, `Towns`, ...), then the places listed
under the map itself, twelve per page. A category opens the level below it, `Back` the one above,
up to the list of the maps. A place takes you there at once and the gump stays open, so you can
hop from one to the next; a place the world refuses, such as one outside its map, says so with
`You cannot go to Arena: its map is not loaded or the spot is outside it.` Long paths and names
are cut at the frame.

The gump is [`templates/gumps/go.xml`](../gumps.md) and its rows come from
`scripts/gumps/go.lua`; both are yours to change. Without `locations.toml`, or when none of its
places is on a loaded map, `.go` alone prints the usage.

## Name the place

`.go <place>` goes to the place the words name, in any case:

- its name: `.go minoc`;
- its categories and its name, as many of the last ones as it takes when several places share a
  name: `.go covetous entrance`, `.go dungeons covetous level 1`;
- a category, which stands for its first place: `.go covetous`, `.go britain`.

Your own map comes first: a place of it, else a category of it, and only then a place or a
category of another map, which takes you to that map. When nothing is named exactly so, the
last words of a name are enough: `.go haven` finds `Old Haven` on a map with no Haven. Only whole
words count. When several places still fit, none is chosen and
the first ten are listed, so you can add a word:

```text
2 places are named entrance; add words of the category, such as go covetous entrance:
Felucca: Dungeons/Covetous/Entrance
Felucca: Dungeons/Shame/Entrance
```

`No place is named atlantis; go alone lists them.` answers a name nothing has.

## Give the spot

An argument that starts with a digit or a sign is a spot. The three numbers are `x`, `y`
and `z`, split by commas or by spaces; `z` goes from -128 to 127. Without a map you stay on your
own; a map is one of the `MapType` names (`Felucca`, `Trammel`, `Ilshenar`, `Malas`, `Tokuno`,
`TerMur`), in any case. Anything else that is not three numbers and at most a map name prints the
usage.

## What happens

You stand on the spot at once: your client is told the new map first when it changes, and the
players around the old spot lose you while those around the new one see you. The height is
taken as it is written; the command does not look for the ground.

A map that is not loaded, or a spot outside the map, is refused with
`You cannot go there: tokuno is not loaded or the spot is outside it.`

## See also

- [All commands](../commands.md)
- [`locations.toml`](../data-files/locations.md)
- [`where`](where.md)
- [`moongate`](moongate.md)
