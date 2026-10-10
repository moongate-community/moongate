## carries

Whether the mobile carries the key of a door:

```lua
world.carries(user, "key.value", 1234)
```

## weather_profile

The name of the weather profile where the player stands:

```lua
local profile = world.weather_profile(user)
```

## set_weather

Forces a kind of weather on that profile until the next game hour, as `.weather` does:

```lua
world.set_weather(user, WeatherKindType.Storm)
```

## season_here

The season the client of a player shows where it stands, the region's else the map's:

```lua
if world.season_here(user) == SeasonType.Winter then
    mobile.message(user, "Brr.")
end
```

## set_season

Sets the season of the map where the player stands until the restart, as `.season` does:

```lua
world.set_season(user, SeasonType.Winter)
```

## clear_season

Gives that map back the season of `maps.toml`:

```lua
world.clear_season(user)
```

## light_here

The light level where a player stands, from 0 (brightest) to 31 (darkest):

```lua
local level = world.light_here(user)
```

## global_light

The level every player was given with `.globallight` or `world.set_global_light`, nil when the light
follows the time of day:

```lua
if world.global_light() == nil then
    mobile.message(user, "The light follows the clock.")
end
```

## set_global_light

Gives every player the same light, as `.globallight` does:

```lua
world.set_global_light(26)
```

## clear_global_light

Makes the light follow the time of day again:

```lua
world.clear_global_light()
```

## moon

```lua
world.moon(MapType.Trammel, x) == MoonPhaseType.FullMoon
```

## time

```lua
world.time(MapType.Trammel, 1600).hours
```

## set_prop

```lua
world.set_prop("event.day", 12)
```

## is_guarded

Whether guards protect the region of the place, such as a town:

```lua
world.is_guarded(MapType.Trammel, 1496, 1628, 10)
```

## is_water

Whether a creature could swim on a cell, as a fishing pole asks of the place a player picked:

```lua
world.is_water(MapType.Trammel, 1496, 1640)
```

## play_sound

A splash on the water, heard by the players around the place:

```lua
world.play_sound(MapType.Trammel, 1496, 1640, -5, 0x364)
```

## statics

Whether a player stands within 2 tiles of an anvil that is part of the map:

```lua
local function near_an_anvil(user)
    local here = mobile.location(user)

    if not here then
        return false
    end

    for _, static in ipairs(world.statics(here.map, here.x, here.y, 2)) do
        if static.graphic == 0x0FAF or static.graphic == 0x0FB0 then
            return true
        end
    end

    return false
end
```

## travel_allowed

Whether the regions of a place let a recall in: false when any region covering it says no:

```lua
if not world.travel_allowed(map, x, y, z, "recall_in") then
    mobile.message_cliloc(user, 1019004) -- You are not allowed to travel there.
end
```
