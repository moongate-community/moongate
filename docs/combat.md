# Combat

A player fights an NPC with its fists, as ModernUO's classic (pre-AOS) combat does; the NPC fights back and
dies when its hit points are gone. This is the first slice: weapons and armor of items, parry, archery,
the death of a player and the combat events of Lua come later.

## Starting a fight

A player clicks a mobile, which sends the attack request (`0x05`), or a script calls
`combat.attack(attacker, target)`. The fight starts when:

- both are in the world, on the same map, and the target is not the attacker, is alive and is not invulnerable
  (`notoriety = "invulnerable"`);
- the target is not hidden from the attacker, is within the view range (`ultima.world.view_range`) and in line
  of sight.

A player goes into war mode and is told whom it fights (`0xAA`). A refused request is answered with a clear
target (`0xAA` with zero), so the client does not keep the target highlighted.

The vendors (every template that inherits `basevendor`), the bankers and the guards are `invulnerable`, with a
yellow name, as in ModernUO: they cannot be attacked. Monsters and animals are grey or red, not blue.

A player who attacks an innocent (a blue name) that is not already fighting it becomes a
[criminal](server-configuration.md), and the guards come. Attacking anyone else, a monster, a criminal or one
that is fighting the player, is no crime.

Peace (war mode off) ends the fight of a player. Any fight ends when the target leaves the world, dies or goes to
another map, after `combatant_seconds` (60) without a swing, or by `combat.stop`.

## The swing

Every tenth of a second a fighter whose delay is over, and whose target is within `max_range` tiles (1) and
15 units of height, swings. The first swing is at once; the delay after each is

```text
15000 / ((stamina + 100) * speed) / global_attack_speed      seconds, speed 30 for fists
```

so 2.5 seconds at 100 stamina. A swing:

1. shows a hidden attacker;
2. costs a player `attack_stamina` stamina (0 by default; UOX3 takes 2);
3. is told to its player (`0x2F`) and plays the swing animation of the body (the punch of a human, the first
   attack of a monster or an animal);
4. rolls the hit with the Wrestling of both, `(attacker + 50) / ((defender + 50) * 2)`, which also teaches the
   Wrestling of a player;
5. on a miss plays the miss sound (`0x239`) and no more;
6. on a hit plays the hit sound (the `attack` sound of the template of an NPC, else the fists' `0x135`) and the
   `hurt` sound of the target, plays its hurt animation and does the damage.

## The damage

- The base is the dice of the template of an NPC (`damage`), or 1 to 8 for a player's fists.
- Raised by the attacker's tactics (`+ (tactics - 50)%`), strength and anatomy (`strength/5%`, `anatomy/5%`, and
  10% more at anatomy 100). Tactics and anatomy are tried at every hit and teach a player.
- Halved when the target is a player or the attacker is an NPC; a player hitting an NPC does all of it.
  `npc_damage_rate` divides what an NPC does to a player.
- The armor of the target, `Armor`, takes one zone's share off, from half to all of it: neck 7%, hands 7%,
  arms 14%, head 15%, legs 22%, chest 35%, the zone being chosen as ModernUO chooses it. At least 1 is done.
- The damage is shown over the target to the players in the fight (`0x0B`, `display_damage_numbers`) and the
  health bar moves.

The status window of a player shows the damage of its fists, `1` to `8` with the same bonuses of tactics, strength and
anatomy (the least is never under 1); the status of an NPC shows none.

An NPC with no hit points left dies as when a game master [kills it](death.md), with the attacker as its killer
and its corpse. **A player does not die yet**: it is left with 1 hit point.

## The NPC fights back

An NPC that is hit, or missed, and fights no one, fights the one who swings, at its own pace. One that fights
another keeps at it. A player that is hit does not fight back by itself: it clicks. The monsters of
[`monster.lua`](scripting/shipped-scripts.md#monsterlua) go for a player and, beside it, `combat.attack` it.

## Settings

`[ultima.combat]`, see [Server configuration](server-configuration.md): `global_attack_speed`, `attack_stamina`,
`npc_damage_rate`, `max_range`, `combatant_seconds` and `display_damage_numbers`.

## Lua

The `combat` module: `combat.attack(attacker, target)`, `combat.stop(mobile)` and `combat.target(mobile)`.

## Not yet

Weapons and armor of items (the damage, speed and armor rating of what is carried), parry, archery, special
moves, durability, aggressor lists beyond the target, the death of a player, bandages and the combat events
of Lua (`attack`, `hit`, `miss`, `get_hit`).

## See also

- [Death of NPCs](death.md)
- [Skills](skills.md)
- [Server configuration](server-configuration.md)
