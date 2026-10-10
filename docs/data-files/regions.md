# Regions

`data/regions/` holds one file per map, named after the map: `felucca.toml`,
`trammel.toml`, `ilshenar.toml`, `malas.toml`, `tokuno.toml` and `termur.toml`. The
file name sets the map of every region in it; a region has no `map` field.

```toml
[[region]]
name = "The Heartwood"
type = "town"
priority = 50
areas = ["(6911, 255)..(7168, 512)"]
go_location = "(6984, 337, 0)"
entrance = "(535, 995, 0)"
music = "ElfCity"
weather = "none"
guarded = true
housing = false
```

| Field | Meaning | Default |
| --- | --- | --- |
| `name` | The region name. Leave it out for areas that only apply rules. | none |
| `type` | `base`, `town`, `dungeon`, `nohousing`, `guarded`, `jail` or `greenacres` (`RegionType`). | `base` |
| `priority` | Where regions overlap, the highest one applies. | 50 |
| `parent` | The name of the region this one is part of, on the same map. | none |
| `areas` | The rectangles of the region. | required |
| `go_location` | Where a "go to region" command takes a character, a `Point3D`. | none |
| `entrance` | The entrance of the town or dungeon, a `Point3D`. | none |
| `music` | The music track, a `MusicType` name such as `Britain1`, played to the players who enter the region; without it, its nearest parent's music plays, else the map's `music`. | none |
| `season` | The season the client shows in the region (`spring`, `summer`, `fall`, `winter`, `desolation`), such as winter in an ice dungeon; it never rotates. Without it, its nearest parent's season applies, else the map's. | none |
| `weather` | The profile of `weather.toml`. | `none` |
| `rune_name` | The name of a rune marked here. | none |
| `guarded` | Whether guards protect the region. Today the player is told so (see [What a player reads](#what-a-player-reads)) and scripts can read it, and a player that says "guards" there brings a guard onto a criminal near it (see [`ultima.crime`](../server-configuration.md)). | `false` |
| `housing` | Whether players may place houses. | `true` |
| `instant_logout` | Whether a character with no fight in progress leaves the world at once on logout. | `false` |
| `recall_in`, `recall_out`, `gate_in`, `gate_out`, `mark`, `teleport_in`, `teleport_out` | Whether those travel spells work into, out of or in the region. | `true` |

`MusicType` (`src/Moongate.Ultima/Types/MusicType.cs`) lists the track names; its
values follow `Music/Digital/Config.txt` of the client.

## Areas

Write each rectangle as `"(x1, y1)..(x2, y2)"`: two corners, not a position
and a size. The first corner is included and the second is excluded. For example,
`"(1330, 1991)..(1343, 2004)"` covers X from 1330 through 1342 and Y from 1991
through 2003. Both points use the same `(x, y)` notation as `Point2D`.

Without height limits, use strings directly:

```toml
areas = [
    "(1330, 1991)..(1343, 2004)",
    "(1494, 3767)..(1506, 3778)",
]
```

To limit height, use an inline table with `bounds` and optional `z1` and `z2`.
`z1` is included and `z2` is excluded. Either limit may be omitted independently;
when neither is present, the rectangle covers every height. Strings and tables
may be mixed in the same `areas` array.

```toml
areas = [
    { bounds = "(1416, 1498)..(1740, 1777)", z1 = -10, z2 = 128 },
    { bounds = "(1500, 1408)..(1546, 1498)", z1 = 0, z2 = 128 },
]
```

The loader also accepts the legacy `{ x1 = ..., y1 = ..., x2 = ..., y2 = ... }`
tables. Serialization always writes the new corner format, using a string when
there are no height limits and a `bounds` table otherwise.

## Parents and overlaps

A `parent` records that a region is part of another, such as a building inside
Britain. Only `music` and `season` are inherited when the file is read: a region
without one takes its nearest parent's. Every region writes out its other rules,
already combined with its parents', so each region can be read on its own.

Where regions overlap, the one with the highest `priority` gives the name, music,
guards, housing and logout rules. Travel works differently: a travel spell is
blocked when any region covering the place blocks it, whatever its priority.

On a tie the child wins over its parent, then the region written first. The server
keeps the region each player stands in and logs a change at debug level
(`"Aria" left Britain for Britain Graveyard`); `.where` prints the region of the spot
you target: `Trammel (1496, 1628, 10) in Britain`.

## What a player reads

Walking into a named place, or being teleported there, a player reads "You have entered Britain.";
walking out, "You have left Britain." Logging in, it reads where it stands once the login is complete.

A place is a named region that is not a mere part of the region around it. A region whose `parent`
has the same `type` and the same `guarded` is a part: a field or a shop inside Britain is still
Britain, and moving between them says nothing. A region that differs from its parent is a place of its
own: New Haven, a guarded town, lies inside Haven Island, which has no guards, so walking in from the
island reads "You have entered New Haven." and arriving from elsewhere names the island first, then the
town. A region of `type = "guarded"`, such as the moongates, only marks guards and is never named. A
place with the same name, such as the same town on another map, is the same place.

When the protection changes a player reads "You are now under the protection of the guards of
Britain." or "You have left the protection of the guards of Britain." The guards bear the name of the
nearest guarded place around the player; where there is none, as at a moongate or in a guarded house of
a town without guards, the client's own two texts are used (500112, 500113). The four texts are
messages 30134 to 30137 of `data/messages`.

What is entered reads in green, what is left in red, the client's own two texts included.

## Travel zones

Travel zones are unnamed regions of priority 0 that only limit travel, such as the
Lost Lands of Felucca:

```toml
[[region]]
type = "base"
priority = 0
areas = ["(5120, 2304)..(6144, 4096)"]
weather = "none"
recall_in = false
recall_out = false
gate_in = false
gate_out = false
mark = false
teleport_in = true
teleport_out = true
```

The server reads a region's type, music, season and weather for the players inside it.
The `guarded` flag is read too: by the guards, the texts of entering and leaving a guarded place, and the vendors. The
travel rules `teleport_in`, `teleport_out`, `recall_in` and `recall_out` are read by the [Teleport and Recall
spells](../magery.md#region-rules): a travel is refused when any region covering the place switches the rule off. No
system reads the housing and logout rules, nor `gate_in`, `gate_out` and `mark`, yet: they are loaded and validated only.

## Validation at startup

The server stops when:

- the `regions/` directory does not exist;
- a file name is not a map name (the match ignores case);
- a region has no areas;
- an area has malformed bounds, missing coordinates, mixed `bounds` and legacy
  coordinate fields, unknown fields, or non-integer height limits;
- the second corner is not above the first on both X and Y, or, when both height
  limits are set, `z2` is not above `z1`;
- a region's `weather` is not a profile of `weather.toml`, or the `weather` of a map in `maps.toml` is not one;
- a name is used twice in the same file;
- a `parent` is not a region of the same file, or the parents loop back.

## Add a region

1. Open the file of the map, such as `data/regions/trammel.toml`.
2. Add a `[[region]]` with its `areas`, and every rule written out, including those
   of its parent. Omitted fields take the defaults of the table above.
3. Give it a `priority` above the regions it should override where they overlap.
4. Use a `weather` profile that exists in `weather.toml`.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
