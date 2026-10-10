# Blacksmithing

A smith standing at an anvil and a forge forges weapons, armor and shields from iron ingots. The rules are the ones of
every craft: see [Carpentry](carpentry.md) for the chance, failures, exceptional items, the maker's mark, tools that
wear out and Make last.

## How to forge

1. Stand within 2 tiles of an anvil and of a forge: ground items, or the anvils and forges the map itself has in its
   smithies.
2. Carry iron ingots in your backpack or a bag of it.
3. Double click a smith's hammer, a sledge hammer or tongs, in your backpack, a bag of it or your hands. The crafting
   gump of blacksmithing opens.
4. Press the button before a recipe, or open its page to see the ingots, the skills and your chance.

Away from an anvil or a forge the gump still opens, but forging answers "You must be near an anvil and a forge to
smith items." and takes nothing. The anvil and the forge are looked for again at the second stroke.

## The recipes

66 recipes in eleven groups, converted from UOX3, every era: Ringmail, Chainmail, Platemail, Helmets, Shields, Bladed,
AOS Weapons, Axes, Polearms, Bashing and SE Weapons. A few of them:

| Recipe | Blacksmithy | Takes |
| --- | --- | --- |
| Buckler | 0 to 10 | 10 metal |
| Ringmail gloves | 12 to 62 | 10 metal |
| Platemail | 75 to 125 | 25 metal |
| Dagger | 0 to 50 | 3 metal |
| Longsword | 28 to 78 | 12 metal |
| War hammer | 34.2 to 84 | 16 metal |

The page of each recipe in the gump shows its numbers. Metal is iron ingots; a few recipes take cloth or Tailoring too.

## Exceptional weapons and armor

An exceptional weapon does 20% more damage; an exceptional piece of armor or shield gives 8 more armor. See
[Combat](combat.md#weapons-and-armor). An exceptional item is uncommon, and rare when it bears the maker's mark: its
tooltip shows the rarity in its colour.

## Change the rules

- The recipes are [`data/crafts/blacksmithing.toml`](data-files/crafts.md).
- The anvils and forges are `scripts/common/smithy.lua`, shared with smelting; what a craft must stand near is the
  table `NEEDS` of `scripts/common/crafting.lua`.
- The tools are the templates with `script_id = "smithing_tool"` (`scripts/items/smithing_tool.lua`).

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/blacksmithing.toml`,
`scripts/common/crafting.lua`, `scripts/common/smithy.lua`, `scripts/items/smithing_tool.lua` and
`scripts/items/ore.lua` (it now reads `smithy.lua`), and the files of the tools: `templates/items/skills/tools/blacksmithy.toml`,
`templates/items/gear/weapons/maces_hammers.toml` and `templates/items/misc/bod_rewards_blacksmith.toml`, or give
`script_id = "smithing_tool"` to your hammers, sledge hammers and tongs.

## Not yet

Coloured metals (dull copper to valorite), repair, smelting items back into ingots, and the special moves of the AOS and
SE weapons.

## See also

- [Carpentry](carpentry.md)
- [Mining and smelting](mining.md)
- [Crafts data files](data-files/crafts.md)
