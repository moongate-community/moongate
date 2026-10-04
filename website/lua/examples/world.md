## carries

Whether the mobile carries the key of a door:

```lua
world.carries(user, "key.value", 1234)
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
