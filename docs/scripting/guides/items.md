# Items

This guide builds one script that creates an item, inspects it, changes it,
tucks some loot into a backpack, and finally equips a weapon on a mobile. Every
step uses the [`item`](../reference/item.md) module; follow the links for exact
signatures.

Items are referenced by **serial** — a plain number returned when you create
them. Template ids (like `dagger` or `backpack`) come from the YAML under
`src/Moongate.Server/Assets/Templates/Items/`.

> [!NOTE]
> All `item.*` mutations run on the game-loop thread. The examples below live
> inside a `world_ready` handler, which is already on the loop — see
> [How scripting works](how-scripting-works.md).

## Create from a template

[`item.create`](../reference/item.md#itemcreate) takes a template id, an amount
and a hue (`0` = the template's default hue). It returns the new serial, or
`nil` when the template is unknown.

```lua
local blade = item.create("dagger", 1, 0)
if not blade then
  log.warn("no dagger template")
  return
end
```

## Inspect with `get`

[`item.get`](../reference/item.md#itemget) returns a table of the item's fields
(or `nil` if the serial is gone). Read its name, graphic, stack amount, hue and
where it lives.

```lua
local info = item.get(blade)
log.info("{0} (item_id {1}) x{2}", info.name, info.item_id, info.amount)
```

## Mutate with `set`

[`item.set`](../reference/item.md#itemset) applies a table of recognised fields
and saves. Here we rename and re-hue the dagger into a named blade.

```lua
item.set(blade, { name = "Frostbrand", hue = 1153 })
```

## Containers

A `backpack` is a container item. Create one, create something to put in it, and
place it with [`item.add_to_container`](../reference/item.md#itemadd_to_container)
at a grid position `(x, y)`.

```lua
local pack = item.create("backpack", 1, 0)
local gem  = item.create("diamond", 1, 0)
item.add_to_container(pack, gem, 0, 0)
```

Read a container back with
[`item.contents`](../reference/item.md#itemcontents), which returns an
array-table of serials:

```lua
for _, serial in ipairs(item.contents(pack)) do
  local held = item.get(serial)
  log.debug("pack holds {0}", held.name)
end
```

## Equip on a mobile

[`item.equip`](../reference/item.md#itemequip) puts an item on a mobile at a
[layer](../reference/enums.md#layer_type). A dagger equips at the `OneHanded`
layer; you can pass the name `"OneHanded"` or the `layer_type.OneHanded`
constant.

```lua
local guard = mobile.create_from_template("warrior_guard_male_npc", 1, 1420, 1690, 0)
if guard and item.equip(guard, blade, layer_type.OneHanded) then
  log.info("armed guard {0} with {1}", guard, item.get(blade).name)
end
```

## The complete script

```lua
-- scripts/main.lua

events.on("world_ready", function()
  -- 1. Create a dagger.
  local blade = item.create("dagger", 1, 0)
  if not blade then
    log.warn("no dagger template")
    return
  end

  -- 2. Inspect it.
  local info = item.get(blade)
  log.info("{0} (item_id {1}) x{2}", info.name, info.item_id, info.amount)

  -- 3. Rename and re-hue it.
  item.set(blade, { name = "Frostbrand", hue = 1153 })

  -- 4. Stash a gem in a backpack.
  local pack = item.create("backpack", 1, 0)
  local gem  = item.create("diamond", 1, 0)
  item.add_to_container(pack, gem, 0, 0)

  for _, serial in ipairs(item.contents(pack)) do
    log.debug("pack holds {0}", item.get(serial).name)
  end

  -- 5. Equip the blade on a freshly spawned guard.
  local guard = mobile.create_from_template("warrior_guard_male_npc", 1, 1420, 1690, 0)
  if guard and item.equip(guard, blade, layer_type.OneHanded) then
    log.info("armed guard {0}", guard)
  end
end)
```

Next, the [Mobiles](mobiles.md) guide goes deeper on the mobile you just armed.
