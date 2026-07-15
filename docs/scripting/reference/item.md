# item

Creates and manipulates items by serial. Backed by `ItemModule`.

Items are referenced by **serial** (a number). Template ids, tags and
categories come from the item templates under
`src/Moongate.Server/Assets/Templates/Items/`. The `hue` argument is a number
(`0` = default / no hue).

> [!WARNING]
> Every mutating function here must run on the game-loop thread. See the
> [thread model](../index.md#thread-model). Off-loop calls log a warning but
> still execute.

## item.create

```lua
item.create(templateId, amount, hue) -> serial | nil
```

Creates an item from the template `templateId` with the given `amount` and
`hue`, persists it and returns its serial. Returns `nil` when the template is
unknown or produces no item.

**Example**

```lua
local blade = item.create("dagger", 1, 0)
if blade then
  log.info("created dagger {0}", blade)
end
```

## item.create_by_tag

```lua
item.create_by_tag(tag, amount, hue) -> serial | nil
```

Creates a random item that carries the given `tag`, then persists it and
returns its serial. Returns `nil` when no template carries the tag. Tags are the
entries under `Tags:` in the item templates (for example `weapons`).

**Example**

```lua
local weapon = item.create_by_tag("weapons", 1, 0)
```

## item.create_by_category

```lua
item.create_by_category(category, amount, hue) -> serial | nil
```

Creates a random item in the given `category`, then persists it and returns its
serial. Returns `nil` when the category is empty. Categories are the
`Category:` field of the item templates (for example `Weapons`).

**Example**

```lua
local gem = item.create_by_category("Gems", 1, 0)
```

## item.get

```lua
item.get(serial) -> table | nil
```

Returns a table of the item's fields, or `nil` when no item has that serial.
The table contains:

| Key | Type | Meaning |
|---|---|---|
| `id` | number | The item's serial. |
| `item_id` | number | The graphic (tiledata) id. |
| `name` | string | The item's name. |
| `amount` | number | Stack amount. |
| `hue` | number | Hue value (`0` = default). |
| `layer` | string or nil | The equipped layer name, or `nil` when not equipped. |
| `container` | number | Serial of the parent container (`0` when none). |
| `mobile` | number | Serial of the mobile it is equipped on (`0` when none). |

**Example**

```lua
local info = item.get(blade)
if info then
  log.info("{0} x{1}", info.name, info.amount)
end
```

## item.set

```lua
item.set(serial, fields) -> boolean
```

Mutates the item from a `fields` table and saves it. Recognised keys are read
and applied; anything else is ignored. Returns `true` on success, `false` when
the serial is unknown or `fields` is missing.

Recognised keys: `amount` (number), `hue` (number), `item_id` (number),
`name` (string).

**Example**

```lua
item.set(blade, { amount = 1, hue = 1153, name = "Frostbrand" })
```

## item.flip

```lua
item.flip(serial) -> boolean
```

Flips the item to its next orientation graphic. Returns `false` when the serial
is unknown or the item has no alternate orientation.

**Example**

```lua
item.flip(blade)
```

## item.delete

```lua
item.delete(serial) -> boolean
```

Deletes the item. Returns `true` when an item with that serial existed.

**Example**

```lua
item.delete(blade)
```

## item.equip

```lua
item.equip(mobile, serial, layer) -> boolean
```

Equips item `serial` on `mobile` at the given `layer`. `layer` is a layer name
(case-insensitive, e.g. `"OneHanded"`) or a [`layer_type`](enums.md) constant.
Returns `false` when the layer, mobile or item is unknown.

**Example**

```lua
if item.equip(guard, blade, "OneHanded") then
  log.info("armed guard {0}", guard)
end
```

## item.unequip

```lua
item.unequip(mobile, layer) -> serial | nil
```

Removes whatever the mobile has equipped on `layer` and returns its serial, or
`nil` when the mobile is unknown, the layer is invalid, or nothing was
equipped there. `layer` accepts a name or a [`layer_type`](enums.md) constant.

**Example**

```lua
local removed = item.unequip(guard, layer_type.OneHanded)
```

## item.equipped

```lua
item.equipped(mobile) -> table of serials
```

Returns the serials of the items equipped on the mobile as an array-table
(empty when the mobile is unknown or wears nothing).

**Example**

```lua
for _, serial in ipairs(item.equipped(guard)) do
  log.debug("equipped: {0}", serial)
end
```

## item.add_to_container

```lua
item.add_to_container(container, serial, x, y) -> boolean
```

Places item `serial` into the container item at grid position `(x, y)`. Returns
`false` when the container or item serial is unknown.

**Example**

```lua
local pack = item.create("backpack", 1, 0)
local gem  = item.create("diamond", 1, 0)
item.add_to_container(pack, gem, 0, 0)
```

## item.remove_from_container

```lua
item.remove_from_container(container, serial) -> boolean
```

Removes item `serial` from the container. Returns `false` when the container or
item serial is unknown.

**Example**

```lua
item.remove_from_container(pack, gem)
```

## item.contents

```lua
item.contents(container) -> table of serials
```

Returns the serials contained in the container as an array-table.

**Example**

```lua
for _, serial in ipairs(item.contents(pack)) do
  log.debug("contains: {0}", serial)
end
```
