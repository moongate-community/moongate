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
| `corpse.worn` | What who died wore that went into the corpse, as `serial:layer` pairs split by commas |
| `corpse.hair`, `corpse.hair_hue` | The hair graphic of who died and its hue, when it had hair |
| `corpse.beard`, `corpse.beard_hue` | The same for its beard |

## The dressed corpse

The corpse of a human, elf or gargoyle body is drawn with what the NPC wore, its hair and its beard,
as ModernUO does: right after the corpse the client is sent the worn items that are still inside
(`0x3C`) and their layers (`0x89`). Take an item out of the corpse and it is no longer drawn on it,
for who sees the corpse from then on. Hair and beard are no items: they cannot be taken. The corpse
is dressed 2 seconds after the death, once the body has fallen: the client plays the fall on the
mobile with what it wears and takes off it whatever a corpse is drawn wearing, so a corpse dressed
at once makes the NPC fall naked. Until then the corpse keeps the time in its prop `corpse.dress_at`. The corpse
of any other body is drawn as the client draws that body dead.

When the `corpse` template is missing, the NPC dies all the same and leaves nothing; the log says so.

A bag that lay in the backpack goes into the corpse with what it holds. The world save writes those
contents again even though they did not change: the database deletes them with the backpack of the
dead NPC, which they were under.

## The death sound

The sound is `death` of the NPC template's [`[mobile.sounds]`](templates.md). A human, elf or
gargoyle body without one dies with one of the four voices of its gender (`0x15A` to `0x15D` male,
`0x150` to `0x153` female). Any other body without a sound dies in silence.

## From Lua

```lua
-- Kills the NPC; the second argument, who did it, is kept on the corpse and may be left out.
mobile.kill(orc, user)
```

`mobile.kill` is false for a player, for a mobile that is not in the world and for an NPC that is
already dying.

The script of the NPC may define `on_death`, which runs after the corpse exists and before the NPC
is removed, so the NPC can still be read. Killed by `.kill`, it runs at once. Killed by a script
(`mobile.kill` inside an `on_think`, an `on_use`, a gump): the corpse, the death and the sound come
at once, and `on_death` and the removal on the next turn of the game loop, because a script cannot
run inside another. Until then the NPC is still in the world, without its things.

- `on_death` must not `wait()`: the NPC is removed all the same, and what comes after the wait finds
  it gone.
- An `on_death` that fails is written in the log and the NPC is removed all the same.
- What the NPC wore runs its own `on_unequip` after the NPC is gone: an item script must stand a
  wearer that is no longer there.

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
- Carving, fame and karma, looting as a crime, loot shared among those who fought.
- Summoned creatures that leave no corpse, and bones.
