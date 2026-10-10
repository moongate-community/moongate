## attack

A guard goes for the criminal it reached; the server swings, hits and kills by its combat rules:

```lua
if combat.target(guard_serial) ~= criminal then
    combat.attack(guard_serial, criminal)
end
```

## stop

Ends the fight of a mobile:

```lua
combat.stop(npc_serial)
```

## target

The serial of whom a mobile fights, nil when it fights no one:

```lua
local target = combat.target(npc_serial)
```

## harm

A trap that burns whoever steps on it, with no one to blame:

```lua
function fire_trap.on_step(serial, user)
    combat.harm(user, 15)
    return true
end
```
