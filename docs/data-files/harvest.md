# Harvest

`harvest.toml` lists what is gathered from the world and runs out: the fish of [fishing](../fishing.md), the wood of [lumberjacking](../lumberjacking.md) and the ore of [mining](../mining.md).

```toml
[[resource]]
id = "fish"
area = 8
amount_min = 5
amount_max = 15
respawn_min_minutes = 10
respawn_max_minutes = 20
```

| Field | Meaning |
| --- | --- |
| `[[resource]]` | One per thing gathered. |
| `id` | The name scripts ask for it by, a lower-case identifier: `harvest.take("fish", map, x, y)`. Each used once. |
| `area` | How many tiles on each side an area is, 1 to 256. Every cell of an area shares its amount. |
| `amount_min` | The least a full area holds, at least 1. |
| `amount_max` | The most a full area holds. |
| `respawn_min_minutes` | The least minutes before an area is full again. 0 when left out: it is full at once. |
| `respawn_max_minutes` | The most, up to 10080 (a week). The same as the least when left out. |

A resource may have kinds, its veins: an area is of one of them.

```toml
[[resource]]
id = "wood"
area = 4
amount_min = 2
amount_max = 4

[[resource.vein]]
id = "plain"
weight = 490

[[resource.vein]]
id = "oak"
weight = 300
```

| Field | Meaning |
| --- | --- |
| `[[resource.vein]]` | One per kind, under its resource. Leave them out for a resource of one kind. |
| `id` | The name scripts get it by, a lower-case identifier: `harvest.vein("wood", map, x, y)` gives `"oak"`. Each used once in its resource. |
| `weight` | How often an area is of this kind, against the weights of the other veins of the resource. 1 to 1000000. |

The server only draws the vein; what a vein gives and what it asks for is the script's, such as the kinds of wood of `scripts/items/axe.lua`.

## How an area lives

Each map is cut in square areas of the resource's size. An area is drawn full, between the two amounts, the first
time a script asks for it. A take lowers it by one. At the first take from a full area the time of its refill is
drawn, between the two respawn times; when it comes the area is drawn full again, all at once. An area of a resource with veins draws its vein each time it is drawn full.

Areas are kept in memory and not saved: after a restart every place is full.

A resource with a bad `id` or one used twice, an area outside 1 to 256, an amount below 1, a least above the
most, a time that is negative or above a week, or a vein with a bad `id`, one used twice in its resource or a weight outside 1 to 1000000 stops the server at startup, naming the file. A file with no `[[resource]]` loads with a warning.

## Without the file

A root made before this file existed has none: nothing is gathered, and the fishing poles say the fish are not
biting. Run `mgctl init`, or copy `data/harvest.toml` from the distribution, and see [Existing roots](../fishing.md#existing-roots) for the poles.

## From scripts

```lua
if harvest.take("fish", map, x, y) then
    -- one fish less in that area
end
```

`harvest.has(id)`, `harvest.amount(id, map, x, y)` and `harvest.take(id, map, x, y)`: see the
[Lua reference](https://moongate.sh/lua/harvest/).
