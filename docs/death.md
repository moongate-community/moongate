# Death of NPCs

An NPC can die: it falls where it stands, leaves its corpse with what it carried, and is gone from
the world. Nothing fights yet, so an NPC dies when a game master kills it with
[`.kill`](commands/kill.md) or a script calls `mobile.kill`. Players do not die yet.

## What happens

1. The corpse is made where the NPC stood and takes its things.
2. The players around see the corpse, then the NPC dying (packet `0xAF`: the client plays the death
   of that body on its own).
3. They hear its death sound.
4. The NPC's script runs `on_death`.
5. The NPC leaves the world. One that came from a spawn region frees its place, and the region
   brings another at its next time.

The console and the log say it: `an orc (0x00000384) died at (1700, 1700, 5) of Felucca, killed by
Giachi`.

## The corpse

The corpse is an item of the template `corpse` (`templates/items/corpse.toml`, graphic `0x2006`): a
container on the ground that cannot be picked up. Double click it to open it and take what is
inside, as with a chest.

| | |
| --- | --- |
| Name | `the remains of an orc` (message 30151, in the server's language) |
| Looks | The body, the facing and the hue of who died |
| Inside | What lay in the NPC's backpack, such as its gold and loot, and what it wore |
| Left out | The backpack itself, hair and beard, what cannot be moved, and newbied or blessed items: they go with the NPC |
| Time | 7 minutes (`decay_minutes` of the template), then it is gone with what nobody took |

The loot of an NPC is rolled into its backpack when it is spawned, so the corpse holds what the NPC
already had.

A corpse is one item: its amount is 1. The body it shows is kept in its props and sent to the client
in the place of the amount, as the client expects of the corpse graphic.

| Prop | Meaning |
| --- | --- |
| `corpse.body` | The body of who died |
| `corpse.direction` | The way it faced, a direction number |
| `corpse.template` | The mobile template of who died, when it had one |
| `corpse.killer` | The serial of who killed it, when someone did |

When the `corpse` template is missing, the NPC dies all the same and leaves nothing; the log says so.

## The death sound

The sound is `death` of the NPC template's [`[mobile.sounds]`](templates.md). A human, elf or
gargoyle body without one dies with one of the four voices of its gender (`0x15A` to `0x15D` male,
`0x150` to `0x153` female). Any other body without a sound dies in silence.

## From Lua

```lua
-- Kills the NPC; the second argument, who did it, is kept on the corpse and may be left out.
mobile.kill(orc, user)
```

`mobile.kill` is false for a player and for a mobile that is not in the world.

The script of the NPC may define `on_death`, which runs after the corpse exists and before the NPC
is removed, so the NPC can still be read:

```lua
function orc.on_death(serial, corpse, killer)
    -- corpse is nil when no corpse was made, killer when nobody killed it.
    if corpse then
        -- One more roll of a table of templates/loots, straight into the corpse.
        item.add_loot(corpse, "bonearmor")
    end
end
```

## What is not there yet

- The death of players: ghost, resurrection, healers and ankhs.
- The dressed corpse of human bodies (packet `0x89`): today a human corpse is drawn naked, and its
  clothes are inside.
- Carving, fame and karma, looting as a crime, loot shared among those who fought.
- Summoned creatures that leave no corpse, and bones.
