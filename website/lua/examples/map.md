## display

A sign that shows the map it holds to whoever uses it:

```lua
function town_map_sign.on_use(serial, user)
    local held = item.get_prop(serial, "map")

    if held then
        map.display(user, held)
    end

    return true
end
```

## set_bounds

Draws on a blank map the 400 tiles around the player:

```lua
local here = mobile.location(user)
map.set_bounds(serial, here.x - 200, here.y - 200, here.x + 200, here.y + 200, 200, 200, here.map)
```

## add_world_pin

Marks on a map the tile where a chest is buried:

```lua
map.add_world_pin(serial, chest_x, chest_y)
map.set_protected(serial, true)
```

## pins

Tells the player how many stops the course plotted on a map has:

```lua
mobile.message(user, "The course has " .. #map.pins(serial) .. " stops.")
```
