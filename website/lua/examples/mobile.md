## animate

The bodies do not share the numbers, so use the names of the body: `HumanAnimationType` (`Bow`, `Salute`,
`Fidget1`, `Spell1`...), `MonsterAnimationType` (`Attack1`, `GetHit`, `Pillage`, `Fidget1`...) or
`AnimalAnimationType` (`Eat`, `Alert`, `LieDown`...); a number works too:

```lua
mobile.animate(who, HumanAnimationType.Bow)
```

## body_type

The kind of the body tells which animations it has:

```lua
if mobile.body_type(who) == BodyType.Human then mobile.animate(who, HumanAnimationType.Eat) end
```

## message

```lua
mobile.message(who, "That is too far away.")
```

## message_cliloc

```lua
mobile.message_cliloc(who, 500867)
```

## set_stats

```lua
mobile.set_stats(who, { hits = 10, strength = 80 })
```

## set_hunger

Feeding a mobile; thirst works the same way:

```lua
mobile.set_hunger(who, mobile.hunger(who) + 3)
mobile.set_thirst(who, mobile.thirst(who) + 3)
```

## weight

```lua
if mobile.weight(who) > mobile.max_weight(who) then ... end
```

## skill

A skill is `{ value, cap, lock }`, in points (`50.5`) with `lock` being `up`, `down` or `locked`; a skill never
trained is 0:

```lua
mobile.skill(who, SkillType.Magery).value
```

## skills

Every skill above 0, as a table of name and value:

```lua
mobile.skills(who).magery
```

## teleport

To another map, by a `MapType` or its name. A player's client is told of the map change (0xBF 0x08) and where
it stands (0x20):

```lua
mobile.teleport(who, x, y, z, "Tokuno")
```
