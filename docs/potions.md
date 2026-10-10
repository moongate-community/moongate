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

Poison and cure potions, and explosion potions, are not drunk yet: they come with poison and with throwing.

## Bonuses and night sight

A stat bonus is added to the base stat: the status shows the sum, combat damage and the weight a player can carry use
it, and the maximum hits and stamina rise with it. Stat gains raise the base. Bonuses and night sight are never saved:
they end when their time is up, when the player logs out, or when the server stops, and a save never writes the hits
or stamina they held above the base maximums. An NPC's maximums do not rise with a bonus.

Scripts give them with `mobile.add_stat_bonus(user, "strength", 10, 120)` and
`mobile.set_night_sight(user, 13, 1200)`; `mobile.stats` gives both the values with the bonuses and the `base_` ones.

## Change the rules

- The potions are `scripts/items/potion.lua`: the table `EFFECTS` maps a template to its effect.
- The templates with `script_id = "potion"` are the ten above, in `templates/items/magic/potions.toml`.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `scripts/items/potion.lua` and
`templates/items/magic/potions.toml`, or give `script_id = "potion"` to your heal, refresh, strength, agility and
night sight potions.

## Not yet

Poison and cure (slice 2), explosion (slice 3), alchemy, the buff bar, potion kegs.

## See also

- [Shipped scripts](scripting/shipped-scripts.md)
- [Combat](combat.md)
