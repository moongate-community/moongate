# Combat

A player fights an NPC with its fists, as ModernUO's classic (pre-AOS) combat does; the NPC fights back and
dies when its hit points are gone. Weapons and armor of items are built; parry and the combat events of Lua are not.

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

- The base is the weapon a player holds (a number from its `damage_min` to its `damage_max`), the dice of the template of
  an NPC (`damage`), or 1 to 8 for fists.
- Raised by the attacker's tactics (`+ (tactics - 50)%`), strength and anatomy (`strength/5%`, `anatomy/5%`, and
  10% more at anatomy 100). Tactics and anatomy are tried at every hit and teach a player. An axe is raised the same
  way by Lumberjacking (`lumberjacking/5%`, and 10% more at 100), which a blow does not try.
- Halved when the target is a player or the attacker is an NPC; a player hitting an NPC does all of it.
  `npc_damage_rate` divides what an NPC does to a player.
- The armor takes its share off. A **player** is hit on a part of the body, chosen as ModernUO chooses it (neck 7%,
  hands 7%, arms 14%, head 15%, legs 22%, chest 35%), and the piece it wears there takes half to all of its
  `armor_rating` off; a part with no armor takes nothing. An **NPC** has one number, the `Armor` of its template, and a
  zone's share of it, from half to all, is taken. At least 1 is done.
- The damage is shown over the target to the players in the fight (`0x0B`, `display_damage_numbers`) and the
  health bar moves.
- A hit that does damage leaves **blood** on the ground: a piece under the one hit and from one to `blood_pieces`
  around it, within a tile, one of the seven graphics of ModernUO's blood (`0x1645`, `0x122A` to `0x122F`), in the hue
  of the creature. They are ground items of the `blood_splash_*` templates and go after `blood_seconds` (5), as
  ModernUO's and Source-X's; the ground items are checked every 5 seconds, so a piece can last that much longer. A
  creature whose template says `blood_hue = -1` does not bleed: the undead and the golems. A player bleeds red. Fists,
  swords and arrows bleed alike; there is no blood from a spell, yet. Blood is for show: it gives way when the pool of
  reserved item serials runs short, so the loot and the split stacks keep theirs, and a piece never lies off the map.

The status window of a player shows the damage of its fists, `1` to `8` with the same bonuses of tactics, strength and
anatomy (the least is never under 1), and of Lumberjacking for an axe; the status of an NPC shows none.

A mobile with no hit points left dies as when a game master [kills it](death.md), with the attacker as its killer
and its corpse: an NPC leaves the world, a [player](death.md#death-of-a-player) stays as a ghost. A ghost does not fight,
and nobody fights it.

## The NPC fights back

An NPC that is hit, or missed, and fights no one, fights the one who swings, at its own pace. One that fights
another keeps at it. An NPC with the prop `combat.passive` set to true never answers a blow: the animals of
`scared_animal.lua` run instead. A creature that fights runs when its hit points fall under its `flee_at` percent (20 for a
monster, 10 for an animal unless the template says; -1 never), see [`creature.lua`](scripting/shipped-scripts.md#commoncreaturelua).
The `archerguard` template is a town guard that shoots. A player that is hit does not fight back by itself: it clicks. The monsters of
[`monster.lua`](scripting/shipped-scripts.md#monsterlua) go for a player and, beside it, `combat.attack` it.

## Settings

`[ultima.combat]`, see [Server configuration](server-configuration.md): `global_attack_speed`, `attack_stamina`,
`npc_damage_rate`, `max_range`, `combatant_seconds`, `display_damage_numbers`, `blood_enabled`, `blood_pieces` and
`blood_seconds`.

## Lua

The `combat` module: `combat.attack(attacker, target)`, `combat.stop(mobile)`, `combat.target(mobile)` and
`combat.range(mobile)`; `combat.armor_rating(mobile)` (the armor rating of what it wears, as the status shows it; 0 for none); and, for what practices on a dummy or a butte, `combat.weapon(mobile)` (the skill, whether it is
a bow, its range, its projectile and ammunition), `combat.swing(mobile, x, y)` (turns and plays the swing, no fight)
and `combat.spend_ammo(mobile)` (takes an arrow or a bolt).

## Weapons and armor

What a player holds in its hands is read from the templates of the items on the one-handed and two-handed layers: the
first with a `damage_max` is the weapon. It gives the swing speed, the damage, the skill of the hit chance
(swordsmanship for a sword, an axe or a pole arm, mace fighting for a mace, fencing for a spear or a dagger) and
the sounds and the swing of its kind:

| Kind | Hit, miss | Swing (one hand, two hands) |
| --- | --- | --- |
| `sword` | `0x23B`, `0x23A` | slash (9, 13) |
| `axe` | `0x232`, `0x23A` | slash (9, 13) |
| `pole_arm` | `0x237`, `0x238` | slash (13) |
| `mace` | `0x233`, `0x239` | bash (11, 12) |
| `fencing` | `0x23B`, `0x238` | pierce (10, 14) |

A weapon with no kind, which UOX3 does not list, is fought with Wrestling and sounds as fists. The defender's hit
skill is the one of its own weapon, Wrestling when it holds none. A bow, a crossbow and a thrown weapon are read and
**not** fought with yet: such a player fights with its fists. An NPC fights with its template, whatever it wears.

A crafted item's quality counts: an exceptional weapon does 20% more damage and a low one 20% less, added to the bonuses
above; an exceptional piece of armor gives 8 more armor and a low one 8 less, never below 0; a shield counts
for nothing yet, exceptional or not. See
[Blacksmithing](blacksmithing.md).

A rider swings with the actions of a mount (one hand, two hands, bow or crossbow) instead of the ones of a walker; see [mounts](mounts.md#fighting-from-the-saddle).

The armor rating of the whole player, which the status window shows, is the armor of each part weighted by the share
of the blows it takes (rounded), and the damage shown there is the weapon's, with the bonuses. The numbers are those of UOX3's
eras, converted by `moongate-convert uox` (see [Migrate from UOX3](uox3-migration.md)); a plain graphic inherits the LBR
numbers, ModernUO's classic ones.

## Archers

An NPC that holds a **bow** or a **crossbow** shoots instead of fighting from beside its target, as ModernUO's and
UOX3's archers do. The range is the weapon's: 10 cells for a bow, 8 for a crossbow. It must see its target: with no
line of sight it keeps the fight but does not shoot, and its [creature script](scripting/shipped-scripts.md#commoncreaturelua)
walks it to where it does. Ranges are counted in squares, the larger of the two differences along X and Y, as the scripts and the line of sight count them: a diagonal neighbour is one cell away. The line of sight is from eye to eye. A shot is a swing like any other: the weapon's speed and the NPC's stamina give the delay,
the hit is rolled with the Archery skill against what the target defends with, the damage is the template's dice
(not the bow's), and the arrow `0x0F42`, or the bolt `0x1BFE`, flies from the shooter to the target, hit or missed.
A human body plays the shooting action of its weapon; a monster body plays its attack. The sound is the creature's own
attack, else the bow's, `0x234` on a hit and `0x238` on a miss. An NPC's ammo is never counted, and it need not stand still.

A **player** holding a bow or a crossbow shoots the same way, with the damage of the weapon (a plain bow's 8 to 41, which
the status window shows), and with these differences, as ModernUO and UOX3 have them:

- Each shot spends **one arrow** (bow) or **one bolt** (crossbow), found in the backpack or in a bag in it. With none,
  nothing flies and the swing is lost: its delay is paid all the same, and no message is shown, as in ModernUO.
- Of the shots, hit or missed, **40 percent** leave an arrow or a bolt on the ground at the target's feet, to be picked
  up.
- The player must have **stood still for a second**: a step, not a turn, within `archery_stand_still_seconds`
  ([`[ultima.combat]`](server-configuration.md)) of the shot delays it. 0 asks for nothing.
- The Archery skill is the one rolled, and the one that gains; a bow in the hands of the target is what it defends with.

## Not yet

Parry (a shield counts for nothing yet), a quiver (ammunition is taken from the backpack), special moves, durability (`max_hits` is kept, not used), the
strength a weapon or armor asks for (`strength_required` is kept, not used), aggressor
lists beyond the target and the combat events of Lua (`attack`, `hit`, `miss`,
`get_hit`).

## See also

- [Death and resurrection](death.md)
- [Skills](skills.md)
- [Server configuration](server-configuration.md)
