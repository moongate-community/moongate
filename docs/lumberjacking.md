# Lumberjacking

A player with an axe chops trees for logs. The Lumberjacking skill decides whether a cut works and grows with
use; the wood of a place runs out and comes back with time.

## How to chop

1. Hold an axe in your hands: one in the backpack answers "The axe must be equipped for any serious wood
   chopping."
2. Double click the axe. You read "What do you want to use this item on?" and get a cursor.
3. Pick a tree within 2 tiles.
4. Your character swings one to three times, most often twice, a swing every 1.6 seconds. The result comes with
   the last swing: 0.9, 2.5 or 4.1 seconds after the first.

You must still be within 2 tiles of the tree, with the axe in your hands, when the last swing lands, and you
chop one tree at a time: a second double click meanwhile does nothing.

| You read | Why |
| --- | --- |
| `You can't use an axe on that.` | What you picked is no tree: the land, a rock, an item or someone |
| `That is too far away.` | The tree is more than 2 tiles away, or you walked away before the last swing |
| `There's not enough wood here to harvest.` | The place has no wood left: try elsewhere, or come back later |
| `You hack at the tree for a while, but fail to produce any useable wood.` | The try failed |
| `You can't place any wood into your backpack!` | Your backpack cannot take the logs, which are lost: the wood is gone from the place all the same |

## What you get

The try is rolled on the Lumberjacking skill between 0 and 100, so the chance of a cut that works is the skill
itself, and the skill may rise at every try. That is for plain wood: the other [kinds of wood](#kinds-of-wood) are harder. A cut that works gives 10 logs, which join the logs already in the
backpack. The axe does not wear out.

These axes chop: the hatchet, the axe, the battle axe, the double axe, the executioner's axe, the large battle
axe, the two handed axe, the ornate axe and the gargish battle axe. The gargish axe, the dual short axes and the practice axes do not, and a war axe is a mace.

## Kinds of wood

A place is of one kind of wood, drawn again each time its wood is back, so the same trees may give oak today and
plain wood tomorrow. About half of the places are of plain wood.

| Kind | Places in a thousand | Lumberjacking it asks for | Its cut is tried between |
| --- | --- | --- | --- |
| Plain | 490 | 0 | 0 and 100 |
| Oak | 300 | 65 | 25 and 105 |
| Ash | 100 | 80 | 40 and 120 |
| Yew | 50 | 95 | 55 and 135 |
| Heartwood | 30 | 100 | 60 and 140 |
| Bloodwood | 20 | 100 | 60 and 140 |
| Frostwood | 10 | 100 | 60 and 140 |

One who has the skill of the kind gets its logs one cut in two, and plain logs the other; one who lacks it always
gets plain logs. A cut for the logs of a kind is harder than a plain one: oak at 65 of skill works one time in two.
The logs of a kind have its colour and pile apart from the others.

## Rare finds

At 100 of Lumberjacking a cut that works may give one more thing with the logs, about one cut in six:

| Find | Cuts in a hundred |
| --- | --- |
| Bark fragment | 10 |
| Luminescent fungi | 3 |
| Switch | 2 |
| Parasitic plant | 1 |
| Brilliant amber | 0.1 |

Nothing is made of them yet.

## Boards

Double click the axe in your hands and pick the logs in your backpack instead of a tree: the whole stack is sawn into boards of the same kind, one for each log, at once and with no skill tried. The logs of a kind ask for the Lumberjacking of the kind, as chopping them does: one who lacks it reads "You cannot work this strange and unusual wood." Logs in a bag of the backpack are sawn too. Logs on the ground, in a chest, in your bank box or on your cursor answer "This item must be in your backpack to be used."

## Kindling

Double click a knife, a dagger or a sword you carry and pick a tree within 2 tiles: you hack one kindling off it, at once and with no skill tried. The place must have wood left, and each kindling takes one cut of it, as a cut of an axe does: a place gives a few, then none until its wood is back. Picking anything else answers "You can't use a bladed item on that!"

## Axes in a fight

One who fells trees hits harder with an axe: 1% more damage for every 5 points of Lumberjacking, and 10% on top at 100, so 30% in all for a master. It counts for every weapon with `weapon_type = "axe"`, the axes that chop and the ones that do not. The skill is not tried by a blow: it grows on trees. See [Combat](combat.md).

## The wood of a place

Each map is cut in areas of 4 by 4 tiles. An area holds 2 to 4 cuts, drawn the first time someone chops there:
the trees of an area share them. A cut that works takes one; a failed try takes none. The area is full again,
all at once, 20 to 30 minutes after the first cut from it.

The areas are kept in memory: after a restart every place is full. The numbers are the resource `wood` of
[`harvest.toml`](data-files/harvest.md), and the kinds of wood its veins.

## Change the rules

The rules are in `scripts/items/axe.lua` (the range, the swings, the logs of a cut, the boards and the table of the rare finds), `scripts/common/woods.lua` (the kinds of wood, shared with [carpentry](carpentry.md)), `scripts/items/blade.lua` (kindling) and `scripts/common/trees.lua` (the graphics that count
as trees). See [Shipped scripts](scripting/shipped-scripts.md#axelua). A template chops with `script_id = "axe"`.

## Existing roots

A root made before lumberjacking existed needs two things. Run `mgctl init`, which adds the script. Then add the
resource `wood` to your `data/harvest.toml` and `script_id = "axe"` to the axe bases of
`templates/items/gear/weapons/axes.toml`, or copy both files from the distribution: `mgctl init` never replaces
a file you may have changed. For boards and kindling, copy `scripts/items/axe.lua` again if you had it already, and give `script_id = "blade"` to your knives, daggers and swords, or copy the files of `templates/items/gear/weapons` from the distribution. For the kinds of wood and the rare finds, copy `templates/items/woods.toml`, `scripts/items/axe.lua`, `scripts/common/trees.lua` and `scripts/common/woods.lua`, and add the `[[resource.vein]]` of the wood to your `data/harvest.toml`: without the veins every place is of plain wood, and without the templates a cut gives plain logs.

## Not yet

What is made of the rare finds. Boards and the kinds of wood are worked by [carpentry](carpentry.md).

## See also

- [Fishing](fishing.md)
- [`harvest.toml`](data-files/harvest.md)
- [Skills](skills.md)
- [Shipped scripts](scripting/shipped-scripts.md#axelua)
