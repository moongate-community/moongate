## attack

A guard goes for the criminal it reached; the server swings, hits and kills by its combat rules:

```lua
if combat.target(guard_serial) ~= criminal then
    combat.attack(guard_serial, criminal)
end
```

## aggress

A curse makes the caster the aggressor of the target without hurting it: an innocent who is not fighting back makes the
caster a criminal, and an NPC fights it:

```lua
combat.aggress(caster, target)
mobile.add_stat_curse(target, "strength", 11, 120)
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
