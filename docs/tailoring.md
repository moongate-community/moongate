# Tailoring

A tailor sews clothes, footwear and leather armor from cloth and leather. The rules are the ones of every craft:
see [Carpentry](carpentry.md) for the chance, failures, exceptional items, the maker's mark, tools that wear out and
Make last.

## How to sew

1. Carry a sewing kit in your backpack, or in a bag inside it. Tailoring needs no workbench.
2. Double click it. The crafting gump of tailoring opens.
3. Press the button before a recipe, or open its page to see the cloth or leather it takes, the skills and your
   chance.

A recipe whose cloth or leather you lack answers "You don't have enough cloth to make that." or "You do not have
sufficient leather to make that." and takes nothing.

## The recipes

50 recipes in eight groups, converted from UOX3. The first of each group:

| Group | Recipe | Tailoring | Takes |
| --- | --- | --- | --- |
| Hats | Skullcap | 0 to 52 | 2 cloth |
| Shirts | Doublet | 0.1 to 50 | 8 cloth |
| Pants | Long pants | 24.8 to 75 | 8 cloth |
| Miscellaneous | Body sash | 4.1 to 54 | 4 cloth |
| Footwear | Sandals | 12.4 to 62 | 4 leather |
| Leather Armor | Leather gorget | 53.9 to 104 | 4 leather |
| Studded Armor | Studded gorget | 78.8 to 129 | 6 leather |
| Female Armor | Leather shorts | 62.2 to 112 | 8 leather |

Cloth is any of the folded and cut cloth; leather is cut leather or piles of hides. An exceptional piece of leather
armor gives 8 more armor, as [blacksmithing](blacksmithing.md#exceptional-weapons-and-armor) says; so does an exceptional hat or
suit that has an armor rating of its own, such as the wizard's hat.

## Change the rules

- The recipes are [`data/crafts/tailoring.toml`](data-files/crafts.md).
- The sewing kits are the templates with `script_id = "tailoring_tool"` (`scripts/items/tailoring_tool.lua`); the rules
  are `scripts/common/crafting.lua`.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/tailoring.toml`,
`scripts/common/crafting.lua` and `scripts/items/tailoring_tool.lua`, and `templates/items/skills/tools/tailoring.toml`,
or give `script_id = "tailoring_tool"` to your sewing kits.

## Not yet

Bone armor and short pants, which UOX3 lists in no menu; kinds of leather (spined, horned, barbed, which the runic kits
would work) and coloured cloth to pick; cutting cloth and hides with scissors, dyeing what
is made, and spinning wool and flax into cloth.

## See also

- [Carpentry](carpentry.md)
- [Blacksmithing](blacksmithing.md)
- [Crafts data files](data-files/crafts.md)
