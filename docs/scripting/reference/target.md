# target

The target cursor: asking the player to click something. A [gump](gump.md) asks *which
option*; a target asks *which thing*.

## target.request

```lua
target.request(serial, selection, on_target) -> boolean
```

Raises the cursor for the player behind `serial` and calls `on_target` with what they picked.

`selection` is `"object"` to pick an entity or `"location"` to pick a spot on the ground.
Anything else is **refused** — the call returns `false` and no cursor appears, rather than
quietly picking one of the two for you.

Returns `false` when the serial names nobody online.

```lua
target.request(player, "object", function(r)
    if r.cancelled then
        return
    end

    chat.say(player, "you clicked " .. r.serial .. " at " .. r.x .. "," .. r.y)
end)
```

> [!IMPORTANT]
> **A player has one cursor.** It is a modal state in the client, so asking for a target while
> one is already up replaces what is on screen. The request it replaced has its callback
> called with `cancelled`, because it can no longer be answered — your script does not have to
> track that.

## The result

| field | meaning |
|---|---|
| `cancelled` | `true` when the player picked nothing |
| `type` | `"object"`, `"location"` or `"cancelled"` |
| `serial` | the clicked entity, or `0` |
| `x`, `y`, `z` | where it was, for both objects and ground |
| `graphic` | the graphic the client reported, when it reported one |

**Check `cancelled` first.** It is the answer players give most often: pressing Escape or
right-clicking dismisses the cursor, and a script that assumes otherwise will act on a serial
of zero and coordinates that mean nothing.

## target.cancel

```lua
target.cancel(serial) -> boolean
```

Takes the cursor down. The pending callback is called with `cancelled`. Returns `false` when
nothing was pending.

## From a command

`.where` is the shipped example: it raises an object cursor and reports what was clicked, to
whoever ran it. Its source is `src/Moongate.Server/Commands/WhereCommand.cs`, and it is the
shortest illustration of the whole loop from C# rather than Lua.
