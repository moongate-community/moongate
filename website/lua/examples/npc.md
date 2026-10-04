## nearby

```lua
for _, other in ipairs(npc.nearby(serial, 8)) do ... end
```

## play_sound

By its id, or by a kind of its template's `[mobile.sounds]`:

```lua
npc.play_sound(serial, 0x69)
npc.play_sound(serial, "idle")
```

## players_in_sight

The nearest player the NPC sees within 16 tiles:

```lua
local prey = npc.players_in_sight(serial, 16, 1)[1]
```

## can_see

With `in_sight` `false` the line of sight is not checked, for what the NPC keeps following once it saw it.

## walk_to

Call it on every think; the NPC takes one step each time:

```lua
guard = {}

function guard.on_think(serial)
    local state = npc.walk_to(serial, 1434, 1699)

    if state == "arrived" then
        npc.say(serial, "All quiet at the bank.")
    end
end
```

To follow someone, pass where it stands on every tick and a `range` of 1 to stop beside it:

```lua
local where = mobile.location(target)
npc.walk_to(serial, where.x, where.y, where.z, 1, true)
```

## wander

`wander.lua` strolls one think in four:

```lua
function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        npc.wander(serial)
    end
end
```

## set_prop

`vega.lua` counts the hellos it hears, across restarts:

```lua
function vega.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        local times = (npc.get_prop(serial, "vega.greeted") or 0) + 1
        npc.set_prop(serial, "vega.greeted", times)
        npc.say(serial, "Meow! That's " .. times .. " hellos.")
    end
end
```
