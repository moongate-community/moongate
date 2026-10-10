# Potions

A player drinks a potion by double clicking it, in the backpack or within 1 tile, in a bag on the ground too ("That
is too far away for you to use"). Drinking needs a free hand: not a two-handed weapon, nor a weapon and a shield ("You must have a free hand to
drink a potion."). One potion of the stack goes, then its effect, with the sound of drinking and, for a human on foot,
the gesture; an empty bottle comes back to the backpack, or to the feet when it is full. A potion that cannot be used
up, such as one held on the cursor, does nothing.

## What each potion does

| Potion | Effect |
| --- | --- |
| Lesser heal, heal, greater heal | 3 to 10, 6 to 20, 9 to 30 hit points; then 10 seconds before another heal potion ("You must wait 10 seconds before using another healing potion."). Not drunk at full health. |
| Refresh, total refresh | A quarter of the stamina, or all of it. Not drunk at full stamina. |
| Strength, greater strength | +10 or +20 strength for 2 minutes, and as many more hit points at most. |
| Agility, greater agility | +10 or +20 dexterity for 2 minutes, and as much more stamina at most. |
| Night sight | Sight in the dark for 15 to 39 minutes, sent again when the player changes region. |

A second strength (or agility) potion while the first lasts is refused: "You are already under a similar effect.";
so is a second night sight. When a bonus ends, the hit points or stamina above the new maximum go.

Explosion potions are thrown, not drunk: see [Explosion](#explosion).

## Poison

A poison potion poisons whoever drinks it, at its level: lesser, regular, greater or deadly. A poisoned mobile loses
hits every few seconds, its health bar turns green, it gets no hits back, and those around read that it looks ill;
heal potions are refused ("You can not heal yourself in your current state.").

| Level | Damage a tick | Every | Ticks |
| --- | --- | --- | --- |
| Lesser | 1 and 2.5% of the hits, 4 to 26 | 3 s | 10 |
| Regular | 1 and 3.125%, 5 to 26 | 3 s | 10 |
| Greater | 1 and 6.25%, 6 to 26 | 3 s | 10 |
| Deadly | 1 and 12.5%, 7 to 26 | 4 s | 10 |

The first tick comes after 3.5 seconds, and half the ticks repeat the damage of the last. A stronger poison replaces a
weaker one; a weaker one changes nothing. The poison wears off ("The poison seems to have worn off."), is cured, or
kills; death ends it at once. It is saved with the character with the ticks it has done: logging out does not end it,
it goes on at the next login. An NPC's poison is not saved. A level 4, lethal, is there for monsters. A hidden poisoned
player is not seen to look ill. Clients older than 7.0 do not draw the green bar.

## Cure

A cure potion cures the poison by a chance; it is used up either way ("That potion was not strong enough to cure your
ailment!"), and refused when the drinker is not poisoned ("You are not poisoned.").

| Cure | Lesser | Regular | Greater | Deadly | Lethal |
| --- | --- | --- | --- | --- | --- |
| Lesser cure | 75% | 50% | 15% | 0% | 0% |
| Cure | 100% | 75% | 50% | 15% | 0% |
| Greater cure | 100% | 100% | 100% | 75% | 25% |

Scripts poison and cure with `mobile.poison(user, 2)`, `mobile.cure(user)` and `mobile.poison_level(user)`.

## Explosion

An explosion potion is not drunk but thrown. Double click it in the backpack or within 1 tile: one potion of the stack is
armed in the backpack ("You should throw it now!"), a cursor opens, and a countdown 3, 2, 1 runs over whoever holds it, seen
by those around, the first number after 0.75 seconds, then one a second. At 0 it explodes where it is: in the hand, at its
holder; one held on a cursor goes off as soon as it is let go.

Throw it within 10 tiles and in sight ("That is too far away.", "Target cannot be seen." keep it in the hand, armed;
double click it again to aim). It flies a tenth of a second a tile and the countdown goes on where it lands.

The blast hurts every living mobile within 2 tiles, the thrower too, with the rules of a blow: harming an innocent that
was not fighting the thrower makes it a criminal, and a death names it as the killer. The other explosion potions within 2
tiles go off with it, without the thrower's Alchemy. A thrower who left the game is blamed for nothing, and one's own
pets are no innocents to a blast nor turn on their master.

| Potion | Damage |
| --- | --- |
| Lesser explosion | 5 to 10 |
| Explosion | 10 to 20 |
| Greater explosion | 15 to 30 |

The thrower's Alchemy adds a tenth of its points; a blast does at most 40. Scripts hurt the same way with
`combat.harm(target, damage, attacker)`.

## Bonuses and night sight

A stat bonus is added to the base stat: the status shows the sum, combat damage and the weight a player can carry use
it, and the maximum hits and stamina rise with it. Stat gains raise the base. Bonuses and night sight are never saved:
they end when their time is up, when the player logs out, or when the server stops, and a save never writes the hits
or stamina they held above the base maximums. An NPC's maximums do not rise with a bonus.

Scripts give them with `mobile.add_stat_bonus(user, "strength", 10, 120)` and
`mobile.set_night_sight(user, 13, 1200)`; `mobile.stats` gives both the values with the bonuses and the `base_` ones.

## Change the rules

- The potions are `scripts/items/potion.lua`: the table `EFFECTS` maps a template to its effect.
- The templates with `script_id = "potion"` are the potions above, in `templates/items/magic/potions.toml`; the
  explosion potions have `script_id = "explosion_potion"` (`scripts/items/explosion_potion.lua`).

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `scripts/items/potion.lua`,
`scripts/items/explosion_potion.lua` and `templates/items/magic/potions.toml`, or give `script_id = "potion"` to the
potions a player drinks and `script_id = "explosion_potion"` to the explosion potions.

## Not yet

Alchemy, poisoned weapons and the Poisoning skill, poisonous monsters, bandages that cure, the buff
bar, potion kegs.

## See also

- [Shipped scripts](scripting/shipped-scripts.md)
- [Combat](combat.md)
