# Mining and smelting

A player with a pickaxe or a shovel digs iron ore out of the rock, and smelts it into ingots at a forge. The
Mining skill decides both and grows with use; the ore of a place runs out and comes back with time.

## How to dig

1. Double click a pickaxe or a shovel, in your hands, in your backpack or lying within reach. You read "Where do
   you wish to dig?" and get a cursor.
2. Pick the rock of a mountain or the floor of a cave within 2 tiles.
3. Your character swings once, and the result comes 0.9 seconds later with the sound of the pick.

You must still be within 2 tiles of the place when the result comes, and you dig one place at a time: a second
double click meanwhile does nothing.

| You read | Why |
| --- | --- |
| `You can't mine there.` | The place is no rock: grass, sand, a road, or a static that is no cave floor |
| `You can't mine that.` | You picked an item or someone |
| `That is too far away.` | The rock is more than 2 tiles away |
| `You have moved too far away to continue mining.` | You walked away before the result |
| `There is no metal here to mine.` | The place has no ore left: try elsewhere, or come back later |
| `Someone has gotten to the metal before you.` | The last ore was taken while you swung |
| `You loosen some rocks but fail to find any useable ore.` | The try failed |
| `Your backpack is full, so the ore you mined is lost.` | Your backpack cannot take the pile: the ore is gone from the place all the same |

## What you get

The try is rolled on the Mining skill between 0 and 100, so the chance of a dig that works is the skill itself,
and the skill may rise at every try. A dig that works gives one pile of iron ore, which joins the pile of its
kind already in the backpack:

| Pile | How often | Ingots it smelts into |
| --- | --- | --- |
| Large | 3 in 4 | 2 for each ore |
| Medium, in two shapes | 1 in 8 | 1 for each ore |
| Small | 1 in 8 | 1 for every 2 ore |

The tool does not wear out.

## Smelting

1. Double click a pile of ore, in your backpack or lying within 2 tiles. You read "Select the forge on which to
   smelt the ore, or another pile of ore with which to combine it."
2. Pick a forge within 2 tiles: one placed as an item, or one that is part of the map.

The whole pile is smelted at once. The try is rolled on the Mining skill between 25 and 75: below 25 a smelt
always fails, from 75 it always works, and the try may raise the skill.

| The smelt | What happens |
| --- | --- |
| Works | The pile becomes iron ingots in your backpack, by its size (the table above); an odd small ore is left. You read "You smelt the ore removing the impurities and put the metal in your backpack." |
| Fails | Half the pile is burnt away, rounded down. A pile of one ore gets smaller instead: a large one becomes medium, a medium one small. You read "You burn away the impurities but are left with less useable metal." |

A single small ore answers "There is not enough metal-bearing ore in this pile to make an ingot." Picking
something that is no forge answers `That is not a forge.`, and a forge more than 2 tiles away "That is too far
away." The ore is taken before the ingots are given: a backpack with no room for them loses the metal, and you read `You have no room in your backpack for the ingots: the metal is lost.` A pile you hold on your cursor, or one inside a chest on the ground, answers "The ore is too far away.": put it in your backpack or on the ground first.

## The ore of a place

Each map is cut in areas of 8 by 8 tiles. An area holds 10 to 34 ore, drawn the first time someone digs there. A
dig that works takes one; a failed try takes none. The area is full again, all at once, 10 to 20 minutes after
the first ore taken from it.

The areas are kept in memory: after a restart every place is full. The numbers are the resource `ore` of
[`harvest.toml`](data-files/harvest.md).

## Change the rules

The rules of a dig are in `scripts/items/pickaxe.lua`: the range, the time, the piles and how often each comes,
the land tiles that are rock and the statics that are a cave floor. Those of a smelt are in
`scripts/items/ore.lua`: the range, the skill, the ingots of each pile and the graphics that are forges. See
[Shipped scripts](scripting/shipped-scripts.md#pickaxelua-and-orelua). A template digs with
`script_id = "pickaxe"` and is smelted with `script_id = "ore"`.

## Existing roots

A root made before mining existed needs three things. Run `mgctl init`, which adds the two scripts. Then add the
resource `ore` to your `data/harvest.toml`, and the scripts to the templates: `script_id = "pickaxe"` to the
pickaxes and the shovels of `templates/items/skills/tools/mining.toml`, `script_id = "ore"` to the four piles of
iron ore of `templates/items/skills/resources/mining.toml`. Or copy the three files from the distribution:
`mgctl init` never replaces a file you may have changed.

## Not yet

The other eight metals, from dull copper to valorite, with a vein of its own for each area; combining two piles
into one; tools that wear out; sand, stone and gems; melting a metal item back into ingots.

## See also

- [Fishing](fishing.md) and [Lumberjacking](lumberjacking.md)
- [`harvest.toml`](data-files/harvest.md)
- [Skills](skills.md)
- [Shipped scripts](scripting/shipped-scripts.md#pickaxelua-and-orelua)
