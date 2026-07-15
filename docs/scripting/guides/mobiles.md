# Mobiles

This guide builds one script that spawns a mobile from a template, reads and
adjusts its skills, and explains how templates decide a mobile's gender,
appearance and equipment. It uses the [`mobile`](../reference/mobile.md) module
and real template ids from
`src/Moongate.Server/Assets/Templates/Mobiles/`.

Mobiles are referenced by **serial**, just like items.

> [!NOTE]
> All `mobile.*` mutations run on the game-loop thread. The examples live inside
> a `world_ready` handler, which is already on the loop — see
> [How scripting works](how-scripting-works.md).

## Spawn from a template

[`mobile.create_from_template`](../reference/mobile.md#mobilecreate_from_template)
takes a template id, a map id and an `(x, y, z)` position. It creates the mobile
**and** its equipment in one call, and returns the new serial (or `nil` when the
template is unknown).

```lua
local guard = mobile.create_from_template("warrior_guard_male_npc", 1, 1420, 1690, 0)
```

`warrior_guard_male_npc` is a real shipped template. It comes fully outfitted —
plate armour and a halberd — because its YAML declares an `Equipment:` list that
the factory resolves and equips on spawn.

## How a template is built

Templates are declarative YAML. The `warrior_guard_male_npc` entry looks like
this (abridged):

```yaml
-   Id: warrior_guard_male_npc
    BaseMobile: base_human_npc
    Strength: 100
    Dexterity: 125
    Intelligence: 25
    Skills:
        Anatomy: 1200
        Tactics: 1200
        Swordsmanship: 1200
    Appearance:
        Body: 400
    Equipment:
      - Item: plate_chest
        Layer: InnerTorso
      - Item: halberd
        Layer: TwoHanded
    LootTableId: guard.warrior
```

A few things happen when this is loaded and spawned:

- **`BaseMobile` inheritance.** `base_human_npc` supplies defaults (a base body,
  skin/hair, a basic shirt-and-shoes outfit). The derived template is merged
  onto it: empty strings fall back to the base, `Tags` concatenate,
  `Equipment` (and `Variants`) are replaced wholesale when the derived
  template declares any, and a numeric stat overrides the base only when it
  differs from the DTO default. So the guard keeps the base human look but
  raises Strength/Dexterity/Intelligence and swaps in plate.
- **Skills** are stored in **tenths**, so `Swordsmanship: 1200` means a skill of
  120.0.
- **Equipment** is resolved item-by-item onto the named
  [layers](../reference/enums.md#layer_type).

## Gender

Every template has a gender, chosen per spawn by the factory:

| Template `Gender` | Result |
|---|---|
| `Male` (the default) | always male |
| `Female` | always female |
| `Random` | a coin-flip between male and female on each spawn |

`warrior_guard_male_npc` uses the default, so it always spawns male; the shard
also ships a separate `warrior_guard_female_npc` with `Gender: Female`. Setting
`Gender: Random` on a template instead lets one id produce both.

## Variants

A template may also declare **variants** — weighted alternates picked once per
spawn. Each variant has a `Weight` and may override the gender, loot table,
appearance and equipment; anything it leaves out falls back to the template.
The factory picks one variant by weight on every spawn, so a single id can yield
a coherent mix of looks:

```yaml
    Variants:
      - Name: veteran
        Weight: 3
      - Name: recruit
        Weight: 1
        Gender: Random
        LootTableId: guard.rookie
```

With these weights a spawn is a veteran three times out of four and a recruit
one time in four. (The shipped guard and undead templates keep things simple and
do not use variants, but the mechanism is always available.)

## Skills: read and write

Read a skill with
[`mobile.get_skill`](../reference/mobile.md#mobileget_skill) and write one with
[`mobile.set_skill`](../reference/mobile.md#mobileset_skill). Values are in
tenths, and the skill can be named or a
[`skill_name`](../reference/enums.md#skill_name) constant.

```lua
local swords = mobile.get_skill(guard, "Swordsmanship")
log.info("swords = {0}", swords) -- 1200 -> 120.0

mobile.set_skill(guard, skill_name.Tactics, 1000) -- 100.0
```

Dump every skill at once with
[`mobile.skills`](../reference/mobile.md#mobileskills):

```lua
for name, value in pairs(mobile.skills(guard)) do
  log.debug("{0} = {1}", name, value)
end
```

## The complete script

```lua
-- scripts/main.lua

events.on("world_ready", function()
  -- Spawn a fully equipped town guard.
  local guard = mobile.create_from_template("warrior_guard_male_npc", 1, 1420, 1690, 0)
  if not guard then
    log.warn("guard template not found")
    return
  end

  local m = mobile.get(guard)
  log.info("{0} the {1} spawned at ({2},{3})", m.name, m.gender, m.x, m.y)

  -- Read a starting skill (stored in tenths).
  log.info("swords = {0}", mobile.get_skill(guard, "Swordsmanship"))

  -- Raise a couple of skills.
  mobile.set_skill(guard, skill_name.Tactics, 1000)   -- 100.0
  mobile.set_skill(guard, "Anatomy", 1000)

  -- Spawn a tougher monster nearby, too.
  local knight = mobile.create_from_template("skeletal_knight_npc", 1, 1425, 1695, 0)
  if knight then
    log.info("skeletal knight {0} spawned", knight)
  end
end)
```

The knight you spawned carries a `undead.knight` loot table — the
[Loot](loot.md) guide shows how loot tables turn into items.
