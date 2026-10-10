# Spells

`spells.toml` lists the 64 spells of [Magery](../magery.md): what each is called, its circle, the words of power, the
reagents, what the target cursor asks for, its flags, its sound and graphics, and the scroll that holds it. What a spell
does is its script, `scripts/spells/<key>.lua`; the mana, the delay and the skill window come from the circle, in the
server. Without the file no spell can be cast.

```toml
[[spell]]
id = 5
key = "magic_arrow"
name = "Magic Arrow"
circle = 1
mantra = "In Por Ylem"
action = 17
reagents = [{ template = "0x0f8c_sulfurous_ash", amount = 1 }]
target = "mobile"
harmful = true
resistable = true
reflectable = true
sound = 0x01E5
effect = 0
effect_duration = 0
projectile = 0x36E4
projectile_speed = 5
prompt = "Select target for magic arrow."
scroll = "0x1f32_magic_arrow_scroll"
enabled = true
```

| Field | Meaning |
| --- | --- |
| `[[spell]]` | One per spell, in the order of the client's spellbook. |
| `id` | The client's number, 1 to 64: the bit of the spellbook and what a cast request names. Once only. |
| `key` | The name of its script and of the spell in scripts: lower-case letters, digits and underscores. Once only. |
| `name` | The name players read. |
| `circle` | 1 to 8. |
| `mantra` | The words of power said over the caster's head. |
| `action` | The animation of a human caster: 16 or 17. |
| `reagents` | The item templates and how many of each a cast takes from the backpack (a scroll holds its own). Each must be an item template. |
| `target` | What the cursor asks for: `none`, `mobile`, `item` or `location`. |
| `harmful` | The spell hurts or curses: its cursor is the harmful one. |
| `resistable` | Resisting Spells may weaken it. |
| `reflectable` | Magic Reflection may turn it back. |
| `cast_delay_scale` | How many times the cast delay of its circle the spell takes, above 0 and at most 10; 1 when left out. Blade Spirits and Summon Creature carry 4, as the classic game slowed them. |
| `sound` | The sound of the spell, 0 for none. |
| `effect`, `effect_duration` | The graphic played on the target and for how long, 0 for none. |
| `projectile`, `projectile_speed` | The graphic that flies from the caster to the target and how fast, 0 for none. |
| `prompt` | The text of the target cursor, empty for a spell with no target. |
| `scroll` | The item template of the scroll that holds the spell; its graphic is what tells a scroll from another. |
| `enabled` | `false` takes the spell out of the game. |

A bad value stops the server at startup, naming the spell. A spell with no `scripts/spells/<key>.lua` loads and says it
is disabled when cast.

The file is generated from UOX3's `spells.dfn`:

```sh
cd tools/convert
uv run moongate-convert uox-spells --source <UOX3>/data/dfndata/spells \
  --items ../../moongate_root/templates/items --destination ../../moongate_root/data
```

The scroll of each spell is the canonical scroll template of its graphic and each reagent is a canonical reagent
template, so the conversion fails when one is gone. The target of the area spells and of the ones that take a rune is
set by the converter, not by UOX3's flags, which flag them as a character.
