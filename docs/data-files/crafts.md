# Crafts

`data/crafts` holds the crafts players make things with, one file a craft (`carpentry.toml`, `blacksmithing.toml`,
`tailoring.toml`, `tinkering.toml`, `fletching.toml`, `cooking.toml`, `cartography.toml`, `alchemy.toml` and `inscription.toml` today), and `resources.toml`, the lists of item templates a recipe may take. See [Carpentry](../carpentry.md) for the rules and
[Blacksmithing](../blacksmithing.md). What a craft must stand near, such as the anvil and the forge of blacksmithing, is not data:
it is the table `NEEDS` of `scripts/common/crafting.lua`, which may name it for some groups only (the oven of Baking, the
fire of Barbecue).

## A craft

```toml
id = "carpentry"
name = "Carpentry"
skill = "carpentry"
sound = 0x023D

[[group]]
name = "Chairs"

[[group.recipe]]
name = "Stool"
item = "0x0a2b_a_stool"
skill_min = 11.0
skill_max = 36.0
resources = [{ resource = "wood", amount = 9 }]
skills = []
```

| Field | Meaning |
| --- | --- |
| `id` | The name scripts open it by, a lower-case identifier. Each craft once. |
| `name` | What its gump shows. |
| `skill` | The main skill of every recipe, named as in `data/skills.toml`. |
| `sound` | Played at each of the two strokes. |
| `[[group]]` | A group of the gump, in order: `name`. |
| `[[group.recipe]]` | A recipe of the group, in order. |
| `name` | What the gump shows. |
| `item` | The item template made. |
| `skill_min` | The least of the main skill to try it: the chance there is one in two. -50 to 150; below 0 it is always tried. |
| `skill_max` | The skill at which it never fails. Not below `skill_min`, at most 150. UOX3 sets some above 100 (the studded tunic, the skull with candle): those never become certain, nor exceptional below `skill_max - 60`. |
| `resources` | What it takes: `resource` is a list of `resources.toml` or an item template, `amount` at least 1. At least one. |
| `skills` | Other skills it asks for: `skill`, `min` (the least to try it) and `max`, which its try is measured against. |
| `spell` | Optional. The key of a spell of `data/spells.toml` the crafter must have in a spellbook it wears or carries in its backpack, else "You don't have that spell!". It must be a key of that file; left out for none. |
| `mana` | Optional, 0 or more. The mana a try takes, checked at the start and at the second stroke, paid once, by a success only. |

## resources.toml

```toml
[[resource]]
id = "wood"
templates = ["0x1bd7_board", "0x1bda_board"]
```

| Field | Meaning |
| --- | --- |
| `[[resource]]` | One list. |
| `id` | The name recipes give it, a lower-case identifier. Each list once. |
| `templates` | The item templates that count for it, at least one. |

`wood` is the plain boards and `metal` the iron ingots: a kind of wood or metal picked in the gump takes the boards or
ingots of that kind instead, as `scripts/common/woods.lua` and `scripts/common/metals.lua` say.

## Loading

The server stops at startup, naming the file, for: an id that is not a lower-case identifier or is used twice, an
unknown skill, a group or recipe without a name, an item or resource that is neither an item template nor a list, an
amount below 1, a recipe that takes nothing, skill bounds outside -50 to 150 (the most from 0) or the least above the most, a `spell` that is not a key of `data/spells.toml` (the spells load before the crafts), a `mana` below 0, or a list
without templates or naming one that does not exist, a craft with no name, with no `[[group]]` or with a group that has no recipe. Without the folder nothing can be crafted.

## Where the files come from

They are converted from UOX3's create menus:

```bash
uv run --project tools/convert moongate-convert uox-crafts --source <UOX3>/data/dfndata/create \
    --items moongate_root/templates/items --destination moongate_root/data/crafts \
    --spells moongate_root/data/spells.toml
```

The converter leaves out the groups that make deeds and the recipe of boards, turns UOX3's tenths of skill into
points, and counts only boards as wood. UOX3 nests its menus (Blacksmithing, Armor, Ringmail): each menu that holds
recipes becomes a group, in the order the menus are met. A recipe's name starts with a capital, and a second recipe of
the same name (a spoon facing the other way) is told apart with a number: "Spoon 2". The tinker's tools recipe makes
the tinker's tools, not UOX3's 10-stone tool kit. A root menu's own recipes (the bows) form a first group, a second root
(the arrows and bolts of the fletching tool) is walked after it, and UOX3's batches (five, twenty, fifty) are left out. An item sold alone and in
stacks under one graphic, as a reagent (0x0f85_ginseng and 0x0f85_10_ginseng), becomes a list of its own (`ginseng`);
an alchemy recipe also takes an empty bottle, which UOX3 leaves out.

Inscription is not in UOX3's menus as data the server can use: `uox-crafts --spells moongate_root/data/spells.toml`
builds it from `spells.toml` (a scroll for each enabled spell, in the group of its circle, the reagents of the spell, a
blank scroll of the list `blank_scrolls`, the mana of the circle and the window of the classic data file), and writes
`spell` and `mana`. Without `--spells` the craft is not rebuilt: the list `blank_scrolls` is still written, and the
converter says that `inscription.toml` was left as it was.
