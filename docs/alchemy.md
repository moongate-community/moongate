# Alchemy

An alchemist grinds reagents with a mortar and pestle and pours each potion into an empty bottle. The rules are the ones
of every craft (see [Carpentry](carpentry.md)): the chance, failures that lose half of the reagents, tools that wear out
and Make last. A potion stacks, so it is never exceptional nor marked and making it does not wear the mortar. What each
potion does is told in [Potions](potions.md).

## How to make a potion

1. Carry a mortar and pestle, the reagents and empty bottles in your backpack.
2. Double click the mortar. The crafting gump of alchemy opens.
3. Press the button before a recipe, or open its page.

## The recipes

20 potions in eight groups, converted from UOX3; each also takes an empty bottle.

| Group | Potions (Alchemy, reagents) |
| --- | --- |
| Agility | Agility 15.1-65 (1 blood moss), Greater Agility 35.1-85 (3) |
| Cure | Lesser Cure 0-50 (1 garlic), Cure 25.1-75 (3), Greater Cure 65.1-115 (6) |
| Explosion | Lesser Explosion 5.1-55 (3 sulfurous ash), Explosion 35.1-85 (5), Greater Explosion 65.1-115 (10) |
| Healing | Lesser Heal 0-50 (1 ginseng), Heal 15.1-65 (3), Greater Heal 55.1-105 (7) |
| Poison | Lesser Poison 0-50 (1 nightshade), Poison 15.1-65 (2), Greater Poison 55.1-105 (4), Deadly Poison 90.1-140 (8) |
| Refresh | Refresh 15.1-65 (1 black pearl), Total Refresh 25.1-75 (5) |
| Strength | Strength 25.1-75 (2 mandrake root), Greater Strength 45.1-95 (5) |
| Nightsight | Nightsight 0-50 (5 spider silk) |

A reagent counts sold one by one or by the ten. The potions made are the ones vendors sell, so they stack together; the
plain potions vendors sell and loot drops work as the named ones (`scripts/common/potions.lua`).

## Change the rules

- The recipes are [`data/crafts/alchemy.toml`](data-files/crafts.md); the reagent lists are in
  `data/crafts/resources.toml`.
- The tool is the template with `script_id = "alchemy_tool"` (`scripts/items/alchemy_tool.lua`).

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/alchemy.toml`,
`data/crafts/resources.toml`, `scripts/items/alchemy_tool.lua`, `scripts/common/potions.lua`,
`scripts/items/potion.lua`, `scripts/items/explosion_potion.lua`, `templates/items/magic/potions.toml` and
`templates/items/skills/tools/alchemy.toml`.

## Not yet

The potions of later eras (conflagration, confusion blast and the others), potion kegs.

## See also

- [Potions](potions.md)
- [Carpentry](carpentry.md)
- [Crafts data files](data-files/crafts.md)
