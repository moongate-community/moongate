## slots_of

How many followers a creature of a template counts for, such as before a spell calls one:

```lua
if pet.followers(caster) + pet.slots_of("airele_summon") > pet.max_followers() then
    return 1049645 -- too many followers
end
```

## refresh

Tell the game the followers of a player changed, so they are counted again and the status shows the new number:

```lua
npc.set_prop(creature, "owner", player)
pet.refresh(player)
```
