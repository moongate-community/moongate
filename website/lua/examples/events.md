## on

```lua
local handle = events.on("character_created", function(e)
    log.info("New character {Name}", e.name)
end)

events.off(handle) -- returns false when the handle is unknown
```
