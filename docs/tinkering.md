# Tinkering

A tinker makes tools, parts, utensils, jewelry and candles from ingots, with gems, beeswax or a skull for some of them. The rules
are the ones of every craft: see [Carpentry](carpentry.md) for the chance, failures, exceptional items, the maker's
mark, tools that wear out and Make last.

## How to tinker

1. Carry tinker's tools or a tool kit in your backpack, or in a bag inside it. Tinkering needs no workbench.
2. Double click them. The crafting gump of tinkering opens.
3. Pick the metal to work with Change, as a [smith](blacksmithing.md#metals) does: iron by default, or a coloured metal
   that asks for as much Tinkering as it asks Blacksmithy of a smith, and colours what is made.
4. Press the button before a recipe, or open its page.

## The recipes

59 recipes in seven groups, converted from UOX3. The first of each group:

| Group | Recipe | Tinkering | Takes |
| --- | --- | --- | --- |
| Tools | Scissors | 14.5 to 55 | 4 metal |
| Parts | Gears | 14.7 to 65 | 2 metal |
| Utensils | Butcher knife | 26.1 to 76 | 2 metal |
| Jewelry | Weddingband | 41.8 to 92 | 1 metal, 1 diamond |
| Miscellaneous | Keyring | 21.8 to 72 | 2 metal |
| More Tools | Froe | 33.2 to 83 | 2 metal |
| Candles | Candelabra | 67.1 to 117 | 4 metal, 3 beeswax |

UOX3's traps are left out: a container cannot be trapped yet. The taxidermy kit shares a tool kit's graphic but is
no tinker's tool. Utensils facing either way have a numbered second recipe (Spoon, Spoon 2). Two recipes whose skill UOX3 writes wrong (the scales
and the heating stand, 63.8 and 64.3 to 114) are fixed by the converter.

## Change the rules

- The recipes are [`data/crafts/tinkering.toml`](data-files/crafts.md).
- The tools are the templates with `script_id = "tinkering_tool"` (`scripts/items/tinkering_tool.lua`); the rules are
  `scripts/common/crafting.lua`.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/tinkering.toml`,
`scripts/common/crafting.lua` and `scripts/items/tinkering_tool.lua`, and `templates/items/skills/tools/tinkering.toml`,
or give `script_id = "tinkering_tool"` to your tinker's tools and tool kits.

## Not yet

Traps on containers, assembling parts into clocks and sextants the way they are used, and the special jewelry of later
eras.

## See also

- [Blacksmithing](blacksmithing.md)
- [Carpentry](carpentry.md)
- [Crafts data files](data-files/crafts.md)
