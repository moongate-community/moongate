## take

A well that gives a bucket of water three times, then runs dry until it fills again. It needs a resource `well`
in `data/harvest.toml`, with an area of 1 so each well has its own water:

```lua
function well.on_use(serial, user)
    local here = item.location(serial)

    if harvest.take("well", here.map, here.x, here.y) then
        item.give(user, "0x0ffa_bucket_of_water")
    else
        mobile.message(user, "The well is dry.")
    end

    return true
end
```

## amount

A fisher that tells a player how the fishing is where it stands:

```lua
function old_fisher.on_speech(serial, speaker, text)
    local here = npc.location(serial)
    local left = harvest.amount("fish", here.map, here.x, here.y) or 0

    if left == 0 then
        npc.say(serial, "Nothing bites here today.")
    else
        npc.say(serial, "The water is alive, friend.")
    end
end
```

## vein

A woodsman that tells a player what wood grows where it stands:

```lua
function woodsman.on_speech(serial, speaker, text)
    local here = npc.location(serial)
    local wood = harvest.vein("wood", here.map, here.x, here.y)

    if wood and wood ~= "plain" then
        npc.say(serial, "Good " .. wood .. " around here, if you know your axe.")
    else
        npc.say(serial, "Plain wood, nothing more.")
    end
end
```
