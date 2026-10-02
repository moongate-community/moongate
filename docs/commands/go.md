# go

Takes you to a place of your map or of another.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `go <x>,<y>,<z> [map]` | No | Yes | GameMaster | Game |

```text
.go 1496,1628,10
.go 5690 569 25
.go 1496,1628,10 Felucca
```

In game only. The three numbers are `x`, `y` and `z`, split by commas or by spaces; `z` goes
from -128 to 127. Without a map you stay on your own; a map is one of the `MapType` names
(`Felucca`, `Trammel`, `Ilshenar`, `Malas`, `Tokuno`, `TerMur`), in any case.

You stand on the spot at once: your client is told the new map first when it changes, and the
players around the old spot lose you while those around the new one see you. The height is
taken as you write it; the command does not look for the ground.

A map that is not loaded, or a spot outside the map, is refused with
`You cannot go there: tokuno is not loaded or the spot is outside it.` Anything else that is
not three numbers and at most a map name prints the usage.

## See also

- [All commands](../commands.md)
- [`where`](where.md)
