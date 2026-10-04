## find

The items of a template a mobile wears and carries, at any depth:

```lua
for _, coins in ipairs(item.find(user, "gold")) do ... end
```

## contents

The items lying directly in a container:

```lua
for _, inside in ipairs(item.contents(bag)) do ... end
```

## in_range

```lua
if item.in_range(serial, user, 2) then ... end
```

## consume

`potion.lua`, the script of a potion that is drunk on a double click:

```lua
potion = {}

function potion.on_use(serial, user)
    item.message(serial, user, "You drink the potion.")
    item.consume(serial)

    return true
end
```

## owner

A cursed ring: once worn, it stays on. A worn item has an owner and lies in no container:

```lua
function ring.can_pick_up(serial, picker)
    -- worn: it has an owner and lies in no container
    if item.owner(serial) == picker and item.container(serial) == nil then
        mobile.message(picker, "The ring will not come off.")
        return false
    end
end
```

## equip

Taken from a chest on the ground, those who look into the chest see it go.

## move_into

Players who have a container on the ground open do not see it change until they open it again.

## start_timer

The timers are kept in the props `timer.<name>`, which `item.set_prop` refuses; splitting a stack leaves
them with the part that is lifted.
