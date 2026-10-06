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
