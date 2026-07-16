# mobile

Creates and manipulates mobiles by serial. Backed by `MobileModule`.

Mobiles are referenced by **serial** (a number). Template ids come from the
mobile templates under `src/Moongate.Server/Assets/Templates/Mobiles/`.

> [!WARNING]
> Every mutating function here must run on the game-loop thread. See the
> [thread model](../index.md#thread-model). Off-loop calls log a warning but
> still execute.

Skill values are stored in **tenths** — `500` means a skill of 50.0.

## mobile.create

```lua
mobile.create(name, map, x, y, z) -> serial
```

Creates a mobile named `name` on `map` at `(x, y, z)` and returns its serial
(assigned on insert).

**Example**

```lua
local guard = mobile.create("Town Guard", 1, 1420, 1690, 0)
```

## mobile.create_from_template

```lua
mobile.create_from_template(templateId, map, x, y, z) -> serial | nil
```

Spawns a mobile from the template `templateId` on `map` at `(x, y, z)`,
creating and equipping any equipment the template defines. Returns the new
serial, or `nil` when the template is unknown.

**Example**

```lua
local knight = mobile.create_from_template("skeletal_knight_npc", 1, 1420, 1690, 0)
```

## mobile.get

```lua
mobile.get(serial) -> table | nil
```

Returns a table of the mobile's fields, or `nil` when no mobile has that serial.
The table contains:

| Key | Type | Meaning |
|---|---|---|
| `id` | number | The mobile's serial. |
| `name` | string | The mobile's name. |
| `map` | number | Map id. |
| `x`, `y`, `z` | number | Position. |
| `direction` | string | Facing direction name. |
| `gender` | string | Gender name (see [`gender_type`](enums.md)). |
| `race` | string | Race name (see [`race_type`](enums.md)). |
| `profession` | number | Profession id. |
| `str`, `dex`, `int` | number | Strength, dexterity, intelligence. |
| `backpack` | number | Serial of the mobile's backpack. |

**Example**

```lua
local m = mobile.get(guard)
if m then
  log.info("{0} at ({1},{2})", m.name, m.x, m.y)
end
```

## mobile.set

```lua
mobile.set(serial, fields) -> boolean
```

Mutates the mobile from a `fields` table and saves it. Recognised keys are read
and applied; anything else is ignored. Returns `true` on success, `false` when
the serial is unknown or `fields` is missing.

Recognised keys:

| Key | Type | Notes |
|---|---|---|
| `name` | string | |
| `str`, `dex`, `int` | number | |
| `profession` | number | |
| `map` | number | |
| `hair_style`, `facial_hair_style` | number | |
| `skin_hue`, `hair_hue`, `facial_hair_hue` | number | Hue values. |
| `gender` | string or number | Name or [`gender_type`](enums.md) constant. |
| `race` | string or number | Name or [`race_type`](enums.md) constant. |
| `direction` | string or number | Direction name or numeric value. |

An unrecognised `gender` / `race` / `direction` value leaves that field
unchanged.

**Example**

```lua
mobile.set(guard, {
  race = "Human",
  gender = "Male",
  str = 100, dex = 90, int = 25,
  skin_hue = 1002,
})
```

## mobile.move

```lua
mobile.move(serial, x, y, z) -> boolean
```

Moves the mobile to `(x, y, z)` on its current map. Returns `false` when the
serial is unknown.

**Example**

```lua
mobile.move(guard, 1425, 1695, 0)
```

## mobile.get_skill

```lua
mobile.get_skill(serial, skill) -> number
```

Returns the mobile's value for `skill` (in tenths), or `0` when the serial or
skill is unknown or the skill is unset. `skill` is a skill name — its display
form (`"Animal Lore"`) or compact form (`"AnimalLore"`), punctuation- and
case-insensitive — or a [`skill_name`](enums.md) constant.

**Example**

```lua
local swords = mobile.get_skill(guard, "Swordsmanship")
log.info("swords = {0}", swords)
```

## mobile.set_skill

```lua
mobile.set_skill(serial, skill, value) -> boolean
```

Sets the mobile's `skill` to `value` (in tenths) and saves. Returns `false`
when the serial or skill is unknown. `skill` accepts a name or a
[`skill_name`](enums.md) constant.

**Example**

```lua
mobile.set_skill(guard, skill_name.Swordsmanship, 900) -- 90.0
mobile.set_skill(guard, "Tactics", 850)
```

## mobile.skills

```lua
mobile.skills(serial) -> table | nil
```

Returns a table of the mobile's skill values keyed by skill name (values in
tenths), or `nil` when the serial is unknown.

**Example**

```lua
for name, value in pairs(mobile.skills(guard)) do
  log.debug("skill {0} = {1}", name, value)
end
```

## mobile.delete

```lua
mobile.delete(serial) -> boolean
```

Deletes the mobile. Returns `true` when a mobile with that serial existed.

**Example**

```lua
mobile.delete(guard)
```
