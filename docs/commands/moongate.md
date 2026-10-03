# moongate

Puts at your feet a moongate to a place of your map or of another.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `moongate <x>,<y>,<z> [map]` | No | Yes | GameMaster | Game |

```text
.moongate 1496,1628,10
.moongate 5690 569 25
.moongate 1496,1628,10 Felucca
```

In game only. The place is written as for [`go`](go.md): `x`, `y` and `z`, split by commas or
by spaces, `z` from -128 to 127, then at most one of the `MapType` names (`Felucca`,
`Trammel`, `Ilshenar`, `Malas`, `Tokuno`, `TerMur`), in any case. Without a map the gate leads
to a place of the map you stand on.

A blue moongate (the `moongate` item template) appears where you stand, glowing (prop
`light = "circle300"`, as ModernUO), and is saved with the world. It keeps the place in its props `teleport.x`, `teleport.y`, `teleport.z` and
`teleport.map`; its script, `scripts/items/moongate.lua`, takes whoever steps onto it there a
second later (see [Item scripts](../scripting.md#item-scripts)). You are standing on it, so
step off and on again to use it. The height is taken as you write it; the command does not
look for the ground.

A map that is not loaded, or a spot outside the map, is refused with
`No moongate can lead there: tokuno is not loaded or the spot is outside it.` and no gate is
made. Anything else that is not three numbers and at most a map name prints the usage.

The gate does not go away by itself, and [`remove`](remove.md) takes NPCs only: no command
deletes a gate yet.

## See also

- [All commands](../commands.md)
- [`go`](go.md)
- [Public moongates](../data-files/moongates.md): the gates of the cities, with a list of destinations
