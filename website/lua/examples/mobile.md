## animate

Some of the names: `HumanAnimationType` has `Bow`, `Salute`, `Fidget1`, `Spell1`; `MonsterAnimationType` has
`Attack1`, `GetHit`, `Pillage`, `Fidget1`; `AnimalAnimationType` has `Eat`, `Alert`, `LieDown`. A number works
too:

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

```lua
mobile.skill(who, SkillType.Magery).value
```

## skills

Every skill above 0, as a table of name and value:

```lua
mobile.skills(who).magery
```

## teleport

A player's client is told of the map change (0xBF 0x08) and where it stands (0x20):

```lua
mobile.teleport(who, x, y, z, "Tokuno")
```

## pathfind_to

A guide that sends who asks the way to the bank of Britain: the player's client walks there by itself, and the
player can stop by walking elsewhere:

```lua
function guide.on_speech(serial, speaker, text)
    if text:lower():find("bank", 1, true) then
        npc.say(serial, "Follow your feet.")
        mobile.pathfind_to(speaker, 1434, 1699, 0)
    end
end
```

## add_stat_bonus

A shrine that makes a player stronger for two minutes, once at a time:

```lua
if not mobile.add_stat_bonus(user, "strength", 10, 120) then
    mobile.message_cliloc(user, 502173) -- You are already under a similar effect.
end
```

## set_night_sight

A torch-bearer's blessing that lets a player see in the dark for twenty minutes:

```lua
if not mobile.set_night_sight(user, 13, 1200) then
    mobile.message(user, "You already have night sight.")
end
```

## has_free_hand

Refuses to drink with both hands full:

```lua
if not mobile.has_free_hand(user) then
    mobile.message_cliloc(user, 502172) -- You must have a free hand to drink a potion.
    return true
end
```

## poison

A trap that poisons whoever opens the chest:

```lua
if mobile.poison(user, 1) == "poisoned" then
    mobile.message(user, "A cloud of green gas rises from the chest!")
end
```

## cure

A healer's blessing that ends a poison:

```lua
if mobile.cure(user) then
    mobile.message_cliloc(user, 500231) -- You feel cured of poison!
end
```

## poison_level

Refuses a deadly poisoned player at the gate:

```lua
local level = mobile.poison_level(user)

if level and level >= 3 then
    mobile.message(user, "Come back when you are cured.")
end
```
