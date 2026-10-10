# Cartography

A cartographer draws maps on blank maps with a pen: a local map, a city map or a sea chart of the land around them,
or a map of a whole world. The rules are the ones of every craft: see [Carpentry](carpentry.md) for the chance,
failures, tools that wear out and Make last; a map is never exceptional nor marked. A map, once drawn, opens and takes a
course of pins as every [map](maps.md) does.

## How to draw a map

1. Carry a mapmaker's pen and blank maps in your backpack.
2. Double click the pen. The crafting gump of cartography opens.
3. Press the button before a recipe, and stay where you want the map centred: it is drawn where you stand when it is
   finished, 1.25 seconds later.

## The recipes

| Recipe | Cartography | Draws |
| --- | --- | --- |
| Local map | 10 to 70 | 64 tiles each side and 2 more a point of skill, on a drawing of 200 |
| City map | 25 to 85 | 64 tiles and 4 more a point of skill, at least 200, on a drawing of 200 to 400 |
| Sea chart | 35 to 95 | 64 tiles and 10 more a point of skill, at least 200, on a drawing of 200 to 400 |
| World map | 39.5 to 99.5 | 20 tiles a point of skill around Britain, wherever you stand, on a drawing of 200 to 400 |
| World map of Ilshenar, Malas, Tokuno, Ter Mur | 39.5 to 99.5 | The whole of that world |

Each takes one blank map. The local, city and sea maps show the facet you stand in, up to its edge. UOX3 also takes a
blank scroll; here that is left to the scribes.
UOX3's skill numbers are off by a digit or past any skill: the converter writes the classic ones instead.

## Change the rules

- The recipes are [`data/crafts/cartography.toml`](data-files/crafts.md).
- The pen is the template with `script_id = "cartography_tool"` (`scripts/items/cartography_tool.lua`); how a map
  is drawn is `scripts/common/cartography.lua`. The pen and ink is a scribe's: it opens [Inscription](inscription.md).
  The mapmaker sells the mapmaker's pen (`mapmakerspen`) and buys it back; a scribe and a mage sell the pen and ink.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/cartography.toml`,
`data/crafts/resources.toml`, `scripts/common/crafting.lua`, `scripts/common/cartography.lua`,
`scripts/items/cartography_tool.lua`, `templates/items/skills/tools/cartography.toml` and
`templates/items/skills/tools/inscription.toml`. If drawing a map fails, the player is told "You could not finish what
you made." and the server logs why.

## Not yet

Treasure maps.

## See also

- [Maps](maps.md)
- [Carpentry](carpentry.md)
- [Crafts data files](data-files/crafts.md)
