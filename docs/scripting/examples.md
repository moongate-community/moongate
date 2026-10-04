# Short examples

This page is part of [Writing Lua scripts](../scripting.md). It collects one-line examples of common calls;
each function is described in the [Lua API reference](https://moongate.sh/lua/).

## Items and containers

The items of a template a mobile wears and carries, at any depth (not what lies in its bank), with
[`item.find`](https://moongate.sh/lua/item/#find):

```lua
for _, coins in ipairs(item.find(user, "gold")) do ... end
```

The items lying directly in a container, with [`item.contents`](https://moongate.sh/lua/item/#contents):

```lua
for _, inside in ipairs(item.contents(bag)) do ... end
```

Whether a mobile is on the map of a ground item and within `range` tiles of it, with
[`item.in_range`](https://moongate.sh/lua/item/#in_range):

```lua
if item.in_range(serial, user, 2) then ... end
```

Whether the mobile wears or carries, in its containers at any depth, an item whose prop `key` is `value`,
such as the key of a door, with [`world.carries`](https://moongate.sh/lua/world/#carries):

```lua
world.carries(user, "key.value", 1234)
```

Three things the item functions do not show to everyone:

- [`item.equip`](https://moongate.sh/lua/item/#equip): taken from a chest on the ground, those who look into
  the chest see it go.
- [`item.move_into`](https://moongate.sh/lua/item/#move_into): no weight or item limit is checked, and players
  who have a container on the ground open do not see it change until they open it again.
- [`item.start_timer`](https://moongate.sh/lua/item/#start_timer): the timers are kept in the props
  `timer.<name>`, which `item.set_prop` refuses; splitting a stack leaves them with the part that is lifted.

## Mobiles

An animation of the mobile's body, with [`mobile.animate`](https://moongate.sh/lua/mobile/#animate). The
bodies do not share the numbers, so use the names of the body: `HumanAnimationType` (`Bow`, `Salute`,
`Fidget1`, `Spell1`...), `MonsterAnimationType` (`Attack1`, `GetHit`, `Pillage`, `Fidget1`...) or
`AnimalAnimationType` (`Eat`, `Alert`, `LieDown`...); a number works too:

```lua
mobile.animate(who, HumanAnimationType.Bow)
```

The kind of the body tells which animations it has, with
[`mobile.body_type`](https://moongate.sh/lua/mobile/#body_type):

```lua
if mobile.body_type(who) == BodyType.Human then mobile.animate(who, HumanAnimationType.Eat) end
```

A system message of the client's own texts, by its number, in the language of that client, with
[`mobile.message_cliloc`](https://moongate.sh/lua/mobile/#message_cliloc):

```lua
mobile.message_cliloc(who, 500867)
```

The mobile's numbers, changed with [`mobile.set_stats`](https://moongate.sh/lua/mobile/#set_stats):

```lua
mobile.set_stats(who, { hits = 10, strength = 80 })
```

How full the mobile is, from 0 (starving) to 20 (full), read and set, kept in that range:

```lua
mobile.set_hunger(who, mobile.hunger(who) + 3)
```

A skill as `{ value, cap, lock }`, in points (`50.5`) with `lock` being `up`, `down` or `locked`; a skill
never trained is 0. And every skill above 0 as a table of name and value:

```lua
mobile.skill(who, SkillType.Magery).value
mobile.skills(who).magery
```

A teleport to another map takes a `MapType` or its name, such as `"Tokuno"`. A player's client is told of
the map change (0xBF 0x08) and where it stands (0x20):

```lua
mobile.teleport(who, x, y, z, "Tokuno")
```

## NPCs

The mobiles near an NPC, nearest first, with [`npc.nearby`](https://moongate.sh/lua/npc/#nearby). Height and
line of sight are not checked:

```lua
for _, other in ipairs(npc.nearby(serial, 8)) do ... end
```

The players an NPC sees, nearest first, `limit` of them at most, with
[`npc.players_in_sight`](https://moongate.sh/lua/npc/#players_in_sight). With `in_sight` `false`,
[`npc.can_see`](https://moongate.sh/lua/npc/#can_see) does not check the line of sight, for what the NPC
keeps following once it saw it:

```lua
local prey = npc.players_in_sight(serial, 16, 1)[1]
```

## The world

The phase of a moon seen from the column `x`, with [`world.moon`](https://moongate.sh/lua/world/#moon):

```lua
world.moon(MapType.Trammel, x) == MoonPhaseType.FullMoon
```

The time of day on a map at the column `x`, with [`world.time`](https://moongate.sh/lua/world/#time); see
`ultima.world.seconds_per_uo_minute`:

```lua
world.time(MapType.Trammel, 1600).hours
```

A value the whole shard keeps across restarts, saved with the world:

```lua
world.set_prop("event.day", 12)
```

Whether guards protect the region of the place, such as a town:

```lua
world.is_guarded(MapType.Trammel, 1496, 1628, 10)
```
