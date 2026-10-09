# Crafts

`data/crafts` holds the crafts players make things with, one file a craft (`carpentry.toml` today), and
`resources.toml`, the lists of item templates a recipe may take. See [Carpentry](../carpentry.md) for the rules.

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
| `skill_min` | The least of the main skill to try it: the chance there is one in two. 0 to 150. |
| `skill_max` | The skill at which it never fails. Not below `skill_min`, at most 150. |
| `resources` | What it takes: `resource` is a list of `resources.toml` or an item template, `amount` at least 1. At least one. |
| `skills` | Other skills it asks for: `skill`, `min` (the least to try it) and `max`, which its try is measured against. |

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

`wood` is the plain boards: a kind of wood picked in the gump takes the boards of that kind instead, as
`scripts/common/woods.lua` says.

## Loading

The server stops at startup, naming the file, for: an id that is not a lower-case identifier or is used twice, an
unknown skill, a group or recipe without a name, an item or resource that is neither an item template nor a list, an
amount below 1, a recipe that takes nothing, skill bounds outside 0 to 150 or the least above the most, or a list
without templates or naming one that does not exist. Without the folder nothing can be crafted.

## Where the files come from

They are converted from UOX3's create menus:

```bash
uv run --project tools/convert moongate-convert uox-crafts --source <UOX3>/data/dfndata/create \
    --items moongate_root/templates/items --destination moongate_root/data/crafts
```

The converter leaves out the groups that make deeds and the recipe of boards, turns UOX3's tenths of skill into
points, and counts only boards as wood.
