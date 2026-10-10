# Cooking

A cook turns flour and water into dough, dough into bread, pies and pizzas, and raw meat and fish
into meals. The rules are the ones of every craft: see [Carpentry](carpentry.md) for the chance, failures, exceptional
items, the maker's mark, tools that wear out and Make last.

## How to cook

1. Carry a skillet, a flour sifter or a rolling pin in your backpack, or in a bag inside it.
2. Double click it. The crafting gump of cooking opens.
3. Press the button before a recipe, or open its page.

Ingredients and Preparation are made anywhere. Baking needs an oven within 2 tiles: "You must be near an oven to bake
that." Barbecue needs a source of heat within 2 tiles (an oven, a fireplace, a campfire, a fire pit, a heating stand, a
brazier or a forge): "You must be near a fire source to cook." Both are checked when you start and again at the second
stroke.

## The recipes

31 recipes in four groups, converted from UOX3, every one from 0 to 100 Cooking:

| Group | Recipes | For example |
| --- | --- | --- |
| Ingredients | 5 | Dough: 1 flour, 1 water |
| Preparation | 8 | Unbaked apple pie: 1 dough, 1 apple |
| Baking | 12 | Bread loaf: 1 dough |
| Barbecue | 6 | Fish steak: 1 raw fish steak |

UOX3 tells sweet dough from dough, and the unbaked pies from one another, by their colour and a hidden number; the
converter names their templates instead. It also bakes each pizza from its own uncooked pizza, and cooks each raw cut
(chicken leg, leg of lamb, ribs) from its own cut, where UOX3 takes any raw meat. A closed sack of flour, bought from a
baker or a miller or in the starting items, counts as flour as it is: UOX3 opens it first.

## Change the rules

- The recipes are [`data/crafts/cooking.toml`](data-files/crafts.md).
- The tools are the templates with `script_id = "cooking_tool"` (`scripts/items/cooking_tool.lua`); the rules are
  `scripts/common/crafting.lua`, and the ovens and sources of heat `scripts/common/heat.lua`.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/cooking.toml`,
`scripts/common/crafting.lua`, `scripts/common/heat.lua`, `scripts/items/cooking_tool.lua` and
`templates/items/skills/tools/cooking.toml`, or give `script_id = "cooking_tool"` to your skillets, sifters and rolling
pins.

## Not yet

A pitcher of water is used up, not left empty, and a sack or bowl of flour goes whole into one dough, where UOX3 gives a
sack twenty uses. Wheat cannot be had yet, so the recipe Sack of flour waits for it. Flour mills, and the recipes of
later eras.

## See also

- [Carpentry](carpentry.md)
- [Fishing](fishing.md)
- [Crafts data files](data-files/crafts.md)
