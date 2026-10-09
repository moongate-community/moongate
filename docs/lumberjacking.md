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
itself, and the skill may rise at every try. A cut that works gives 10 logs, which join the logs already in the
backpack. The axe does not wear out.

Every axe chops: the hatchet, the axe, the battle axe, the double axe, the executioner's axe, the large battle
axe, the two handed axe, the ornate axe and the gargish battle axe. A war axe is a mace and chops nothing.

## The wood of a place

Each map is cut in areas of 4 by 4 tiles. An area holds 2 to 4 cuts, drawn the first time someone chops there:
the trees of an area share them. A cut that works takes one; a failed try takes none. The area is full again,
all at once, 20 to 30 minutes after the first cut from it.

The areas are kept in memory: after a restart every place is full. The numbers are the resource `wood` of
[`harvest.toml`](data-files/harvest.md).

## Change the rules

The rules are in `scripts/items/axe.lua`: the range, the swings, the logs of a cut and the graphics that count
as trees. See [Shipped scripts](scripting/shipped-scripts.md#axelua). A template chops with `script_id = "axe"`.

## Existing roots

A root made before lumberjacking existed needs two things. Run `mgctl init`, which adds the script. Then add the
resource `wood` to your `data/harvest.toml` and `script_id = "axe"` to the axe bases of
`templates/items/gear/weapons/axes.toml`, or copy both files from the distribution: `mgctl init` never replaces
a file you may have changed.

## Not yet

Logs into boards, kindling from a tree with a knife, the wood types (oak, ash, yew and the rarer ones), the rare
finds, and the axes' bonus in a fight from the skill.

## See also

- [Fishing](fishing.md)
- [`harvest.toml`](data-files/harvest.md)
- [Skills](skills.md)
- [Shipped scripts](scripting/shipped-scripts.md#axelua)
