# Bowcraft and fletching

A bowyer makes bows and crossbows from boards, shafts from boards, and arrows and crossbow bolts from shafts and
feathers. The rules are the ones of every craft: see [Carpentry](carpentry.md) for the chance, failures, exceptional
items, the maker's mark, tools that wear out and Make last.

## How to make bows and arrows

1. Carry fletcher's tools in your backpack, or in a bag inside it. Bowcraft needs no workbench.
2. Double click them. The crafting gump of bowcraft and fletching opens.
3. Pick the wood with Change, as a [carpenter](carpentry.md) does: plain boards by default, or a kind of wood that asks
   for as much Bowcraft/Fletching as it asks Carpentry of a carpenter, and colours the bow.
4. Press the button before a recipe, or open its page. Make last repeats it: an arrow is made one at a time.

## The recipes

9 recipes in four groups, converted from UOX3:

| Group | Recipe | Bowcraft/Fletching | Takes |
| --- | --- | --- | --- |
| Weapons | Bow | 30 to 70 | 7 wood |
| Weapons | Crossbow | 60 to 100 | 7 wood |
| Weapons | Heavy crossbow | 90 to 130 | 10 wood |
| Weapons | Composite bow | 70 to 100 | 7 wood |
| Weapons | Repeating crossbow | 90 to 100 | 10 wood |
| Weapons | Yumi | 90 to 100 | 10 wood |
| Shafts | Shaft | 0 to 40 | 1 wood |
| Arrows | Arrow | 0 to 40 | 1 shaft, 1 feather |
| Crossbow Bolts | Bolt | 0 to 70 | 1 shaft, 1 feather |

UOX3's batches of five, twenty and fifty are left out: Make last makes them one after the other. Kindling is left out
too: an [axe](lumberjacking.md) already cuts it from logs.

## Change the rules

- The recipes are [`data/crafts/fletching.toml`](data-files/crafts.md).
- The tools are the templates with `script_id = "fletching_tool"` (`scripts/items/fletching_tool.lua`); the rules are
  `scripts/common/crafting.lua`.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/fletching.toml`,
`scripts/items/fletching_tool.lua` and `templates/items/skills/tools/fletching.toml`, or give
`script_id = "fletching_tool"` to your fletcher's tools.

## Not yet

Making a number of arrows at once, and the bows of later eras.

## See also

- [Carpentry](carpentry.md)
- [Lumberjacking](lumberjacking.md)
- [Crafts data files](data-files/crafts.md)
