# Death and resurrection

An NPC can die: it falls where it stands, leaves its corpse with what it carried, and is gone from
the world. An NPC dies when its hit points are gone in a [fight](combat.md), when a game master kills it with
[`.kill`](commands/kill.md), a script calls `mobile.kill`, or a [town
guard](scripting/shipped-scripts.md#guardlua) reaches it while it is a criminal. A [player dies too](#death-of-a-player), and stays as a ghost.

## What happens

1. The players around hear its death sound.
2. The corpse is made where the NPC stood and takes its things.
3. The players around see the corpse, then the NPC dying (packet `0xAF`: the client plays the death
   of that body on its own).
4. The NPC's script runs `on_death`.
5. The NPC leaves the world. One that came from a spawn region frees its place, and the region
   brings another at its next time.

A human, elf or gargoyle body dies in two moments, 1.5 seconds apart: first it plays its fall
(action 21, as [`animate 21`](commands/animate.md) shows it) and cannot move; when the fall is over
come its corpse, `on_death` and its removal, with no `0xAF`. See [The dressed
corpse](#the-dressed-corpse) for why. An NPC removed while it falls leaves no corpse.

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
| `corpse.name` | The name of who died |
| `corpse.spawn.region`, `corpse.spawn.x1` … | What its spawn region gave who died: the region and the four corners of its home |
| `corpse.killer` | The serial of who killed it, when someone did |
| `corpse.worn` | What who died wore that went into the corpse, as `serial:layer` pairs split by commas |
| `corpse.pet_owner`, `corpse.pet.*` | The owner of a bonded pet that died and the props of the pet (loyalty, bond) that go with it; see [Animal taming](animal-taming.md#bonding-and-raising-a-pet) |
| `corpse.hair`, `corpse.hair_hue` | The hair graphic of who died and its hue, when it had hair |
| `corpse.beard`, `corpse.beard_hue` | The same for its beard |

## The dressed corpse

The corpse of a human, elf or gargoyle body is drawn with what the NPC wore, its hair and its beard,
as ModernUO does: right after the corpse the client is sent the worn items that are still inside
(`0x3C`) and their layers (`0x89`). Take an item out of the corpse and it is no longer drawn on it,
for who sees the corpse from then on. Hair and beard are no items: they cannot be taken. The corpse
of any other body is drawn as the client draws that body dead.

When the `corpse` template is missing, the NPC dies all the same and leaves nothing; the log says so.

A bag that lay in the backpack goes into the corpse with what it holds. The world save writes those
contents again even though they did not change: the database deletes them with the backpack of the
dead NPC, which they were under.

The fall is played by the server, not by the death packet. ClassicUO, since 2019, takes the clothes
off a mobile that dies by `0xAF`: it gives the dying mobile a serial of its own, counts its worn
items as lying on the ground far away and deletes them at once, so the body falls naked whatever a
server sends; the other emulators all show it so. An action the mobile is told to play is drawn
dressed, so a human body is told to fall, and its corpse, dressed, takes its place when the fall is
over. UOX3 plays the same action for an NPC killed by a guard.

## The death sound

The sound is `death` of the NPC template's [`[mobile.sounds]`](templates.md). A human, elf or
gargoyle body without one dies with one of the four voices of its gender (`0x15A` to `0x15D` male,
`0x150` to `0x153` female). Any other body without a sound dies in silence.

## From Lua

```lua
-- Kills the NPC; the second argument, who did it, is kept on the corpse and may be left out.
mobile.kill(orc, user)
```

`mobile.kill` kills a player too, who stays as a ghost. It is false for a mobile that is not in the world, a
player that is dead already, a body without a ghost and an NPC that is already dying.

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

## Raising who died

A corpse can give its NPC back: a game master targets it with [`.resurrect`](commands/resurrect.md),
or a script calls `mobile.resurrect(corpse)`.

1. An NPC of the corpse's mobile template (`corpse.template`) is born where the corpse lies.
2. It takes the name and the facing of who died and, when it came from a spawn region, that region
   and its home, so it counts for the region and wanders where the old one did.
3. A human, elf or gargoyle body rises with its fall played backwards.
4. The corpse is gone, with what was left inside.

It is a new NPC of the same kind, not the old one back: it comes with the equipment and the loot of
its template, and has another serial. Its script runs `on_spawn` before it takes the name of who
died. One removed or killed in the moment between its birth and its rising is not shown rising, and
the corpse stays. What was taken from the corpse stays taken. A corpse of an NPC
made without a template, or whose template no longer exists, cannot be raised and stays where it is.

```lua
-- True when the serial is a corpse: the NPC is born on a later turn of the game loop.
mobile.resurrect(corpse)
```

## Death of a player

A player dies when something takes its last hit point (a fight, `.kill`, `mobile.kill`). There is no saved
"dead" flag: a player is dead while it wears the **ghost body** of its race and gender (human 400/401 to
402/403, elf 605/606 to 607/608, gargoyle 666/667 to 694/695), which is what `mobile.is_dead(serial)` reads.

1. The players around hear its death sound; a criminal is pardoned.
2. Its corpse lies where it stood, as an NPC's, with what it wore and what lay in its backpack, except what
   cannot move and the newbied and blessed items. The backpack, hair and beard stay with the player.
3. A rider is dismounted first: its horse stands where it falls. Everyone around sees it die (`0xAF`); the player's own client gets the death status (`0x2C`).
4. War mode is off and hit points, stamina and mana are 0.
5. The player takes the ghost body and puts on a **death shroud** (`death_shroud`, outer torso layer, cannot be
   taken off).

A ghost:

- is **hidden from the living**, as a hidden player, unless it is in war mode; the staff and the other ghosts of the
  staff see it. A step does not show it, only war mode does.
- is heard by the living as `oOo` for each word it says; the staff and the dead hear it as it spoke, and no NPC,
  item or guard answers it.
- does not fight and is not fought; NPCs do not sense it.
- uses no skill, lifts no item and gets no hit point, mana or stamina back. A double click on an item runs its
  `on_ghost_use(serial, user)` if the script has one, and says `I am dead and cannot do that.` if not.

### Coming back

- An **ankh** (the two pieces of each `AnkhWest` and `AnkhNorth` that `.decorate` places, template
  `decoration_ankh`, script `ankh.lua`): a ghost that double clicks it from 2 cells or closer is asked in a gump
  whether it wants to live; Continue raises it with the sound `0x214` and the sparkles `0x376A`, and it must still
  be there, dead and within 2 cells when it answers.
- A **healer** (templates `healer`, `m_healer`, `f_healer`, and the wandering `whealer`, `m_whealer`,
  `f_whealer`, script `healer.lua`): a ghost that comes within 4 cells of one, with the healer in sight, is
  offered the same gump, with the healer's sound `0x1F2` and the sparkles. A healer offers every 2 seconds at most
  and only to a ghost that comes near: it must leave and come back to be offered again. A ghost met while the healer waits for its turn is offered when the wait is over, which ModernUO does not do. The ghost of a game master is offered too. A criminal is
  refused ("Thou art a criminal. I shall not resurrect thee."), and one of negative karma is told it has strayed
  and offered all the same. It costs nothing, and the ghost may answer from up to 8 cells away.
- A clean bandage on a ghost, from a healer with 80 points of Healing and Anatomy (a chance of (Healing - 68) / 50), asks the ghost
  the same way; the wait is 5 seconds longer than for a wound.
- A game master with [`.resurrect`](commands/resurrect.md), a script with `mobile.resurrect(serial)`.

A resurrection by ankh or healer costs a tenth of the player's **fame**, as ModernUO; `.resurrect` and
`mobile.resurrect` do not.

The player comes back in its living body with **10 hit points**, full stamina and no mana, the shroud is gone and a
**death robe** (`death_robe`, hue 2301, newbied so it never goes into a corpse) is worn in its place. Its corpse is
not given back: it lies on its own until it decays.

```lua
mobile.is_dead(serial)   -- true for a player that is a ghost
mobile.resurrect(serial) -- the serial of a ghost raises it at once
```

## Murder counts

A player that attacks an innocent player who does not fight back is a criminal, and the attack is noted. When that
victim dies, a few seconds later (`report_delay_seconds`) it is asked in a gump, for each of those who attacked it as
a criminal in the last two minutes, whether to report them as a murderer; the gump closed or answered No reports
nobody. Nothing is counted by the death itself, as ModernUO.

- **Yes** adds a **kill** (long term) and a **short-term murder** to the killer, and sets its karma to −1000 for
  each kill. It reads "You have been reported for murder!" and at **five kills** "You are now known as a murderer!":
  its name is red for everyone. The same victim cannot report the same killer again for 10 minutes.
- Both counts are forgotten one at a time, the short-term murders after 8 hours and the kills after 40, counted from
  the last report, in real time: hours spent out of the world count too (ModernUO counts online time), and the
  counts are brought up to date while the player is in the world and when it comes back. A player is red while it has five kills
  or more.
- A red player is wanted by the guards, as a criminal is, and a healer refuses it. See
  [`[ultima.murder]`](server-configuration.md) for the times.
- **Resurrecting costs a murderer**: from five short-term murders, an ankh or a healer takes 5 to 15% of each stat
  and of each skill (`100 − (4 + murders/5)` percent kept, never under 85% nor over 95%), but leaves a stat at 10 or
  a skill at 35 points untouched when it would fall under it.
- **Looting is a crime**: taking an item from the corpse of an innocent player (one that was no criminal and no
  murderer when it died) makes the looter a criminal, unless it is the corpse's owner or staff. The corpse of a
  criminal or a murderer is free. ModernUO does this outside Trammel only; here there is no Trammel rule yet.

The counts are saved with the player. `mobile.murders(serial)` reads them as `{ kills, short_term }` and
`mobile.is_murderer(serial)` says whether it is red.

An **evil healer** (`evilhealer`, `evilwhealer`, the id starts with `evil`) turns nobody away: it raises the red
players and the criminals too, as ModernUO's before AOS. The data places none yet.

## What is not there yet

- Spells that raise, and the evil healers' places in the world. A ghost is not seen by other ghosts. Bones: a
  corpse just decays. A party or a guild that may loot a corpse, and the bounty a victim can put on a murderer, as
  ModernUO: none of them exist here.
- A player that attacks a player at once back is no criminal and cannot be reported, as ModernUO; but the "reportable"
  list is kept in memory: a restart forgets it.
- Whether the spot is free for a body (ModernUO's `Map.CanFit`) is not checked when an ankh or a healer raises a
  ghost.
- Carving, fame and karma, loot shared among those who fought.
- Summoned creatures that leave no corpse, and bones.
