## execute

A script that keeps the world saved before it changes the season, as the console would:

```lua
commands.execute("save")
commands.execute("season", "winter")
```

A timer that tells everyone the server is about to save:

```lua
timer.every(3600, function()
    commands.execute("broadcast", "The", "world", "is", "being", "saved.")
    commands.execute("save")
end)
```

## execute_as

A stone that sends who double clicks it to Britain with the staff's own `go`; a player whose account may not
use that command reads the refusal it would read typing it:

```lua
function travel_stone.on_use(serial, user)
    commands.execute_as(user, "go", "britain")
    return true
end
```
