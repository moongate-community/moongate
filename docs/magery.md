# Magery

A mage casts the spells of Magery from a spellbook or reads them from a scroll, by the rules of the classic game: words
of power, a delay in which the caster stands still, a target cursor, reagents, mana and a skill check. It has the
spellbook, the casting engine, the first four circles and Reactive Armor; the other circles follow.

## The spellbook

- A spellbook holds up to 64 spells, as the client numbers them: eight circles of eight.
- A double click opens it, in hand or in the backpack (not in a bag inside it). The spells it holds show in the client's
  window; a click on one casts it.
- A scroll dropped on a book carried by its owner writes its spell in it, with a sound. One scroll of a stack is used
  up. A spell the book holds already is refused ("That spell is already present in that spellbook.") and the scroll
  goes back.
- The spells of a book are a number kept in the item, `spellbook.spells`; a book that has none holds what the `spells`
  tag of its template says. `spellbook` is empty, `spellbook1` to `spellbook1to8` hold the first circle to the first
  eight, and `spellbook_full` holds all 64. Staff give one with `.add spellbook_full`.

## Casting

The cast comes from the client's spell icon, a macro, the book's window or a double click on a scroll (kinds 0x27 and 0x56
of the text command and the extended command 0x1C of the client).

1. It is refused, with the classic text, when the caster is dead, already casting, frozen, not yet recovered from the
   last cast or without the mana of the circle, or when no spellbook it wears or carries holds the spell.
2. The caster says the words of power over its head, makes the gesture of the spell (not on a mount) and cannot move for
   the delay of the circle: 0.5 seconds for the first, a quarter of a second more for each next.
3. When the delay ends the target cursor comes (12 tiles, in line of sight), or the spell takes effect at once when it asks for
   none. Putting the cursor away ends the cast and costs nothing. After the delay the caster waits 0.75 seconds before
   another cast.
4. On the target the cast takes the reagents from the backpack (a scroll holds its own), the mana again and tries
   Evaluating Intelligence, which may grow from every try, and Magery. A success pays the mana, uses up one scroll and
   runs the spell. A failure fizzles: the reagents are lost, the mana is not, a scroll stays.
5. Damage taken while the delay runs, a blow or a tick of poison, ruins a spell above the first circle: the caster is
   told, and waits longer the less of the delay was done, from a second down to a fifth of one. The first circle is
   never ruined, and a cast waiting for its target is not. A caster that dies, of whatever, casts no more.

| Circle | Mana | Delay | Magery window of a book | Magery window of a scroll |
| --- | --- | --- | --- | --- |
| 1 | 4 | 0.5 s | 0 to 40 | -50 to -10 |
| 2 | 6 | 0.75 s | 10 to 50 | -30 to 10 |
| 3 | 9 | 1.0 s | 20 to 60 | 0 to 40 |
| 4 | 11 | 1.25 s | 30 to 70 | 10 to 50 |
| 5 | 14 | 1.5 s | 40 to 80 | 20 to 60 |
| 6 | 20 | 1.75 s | 50 to 90 | 30 to 70 |
| 7 | 40 | 2.0 s | 60 to 100 | 40 to 80 |
| 8 | 50 | 2.25 s | 70 to 110 | 50 to 90 |

## The first circle

| Spell | Reagents | What it does |
| --- | --- | --- |
| Clumsy | blood moss, nightshade | Lowers the dexterity of the target by 1 and a tenth of the caster's Magery, for 1.2 seconds a point of it |
| Create Food | garlic, ginseng, mandrake root | A random food in the backpack, or at the feet when it is full |
| Feeblemind | ginseng, nightshade | As Clumsy, on the intelligence (and the mana maximum of a player) |
| Heal | garlic, ginseng, spider silk | A tenth of the Magery and 1 to 5 hit points; refused for a poisoned target, a dead one or one at full hits |
| Magic Arrow | sulfurous ash | After half a second, 4 to 7 fire damage, three quarters of it when resisted, scaled by Evaluating Intelligence against Resisting Spells and by Magery, doubled against a monster or an animal |
| Night Sight | sulfurous ash, spider silk | Sees in the dark for 15 to 39 minutes, as bright as the Magery says (26 at 100) |
| Weaken | garlic, nightshade | As Clumsy, on the strength: the maximum hits of a player fall with it |
| Reactive Armor | garlic, spider silk, sulfurous ash | For 25 seconds and half a second a point of the caster's Magery, a share of every melee blow that lands from arm's length goes back to its attacker: 10 per cent and a quarter of a per cent a point of the Magery of the wearer when it is hit (35 at 100). An arrow does not go back, a guard is never hurt by it, and an attacker that falls to it ends the blow |

A harmful spell makes the caster the aggressor of its target: a criminal against an innocent who does not fight it, and
an NPC fights back. A curse of a stat that is as strong or stronger stays. A harmful spell on a target that cannot be
harmed, such as a vendor or a banker, is refused with "You cannot perform negative acts on your target." before the
reagents and the mana are spent.

A prisoner in [jail](jail.md) casts no spell: it reads "You cannot cast spells here.", so it cannot Recall or Teleport out.
Staff is never held to it.

## The second circle

| Spell | Reagents | What it does |
| --- | --- | --- |
| Agility | blood moss, mandrake root | Raises the dexterity by 1 and a tenth of the caster's Magery, for 1.2 seconds a point of it |
| Cunning | mandrake root, nightshade | As Agility, on the intelligence |
| Strength | mandrake root, nightshade | As Agility, on the strength |
| Cure | garlic, ginseng | May end a poison: the chance is (10000 + 75 a point of Magery - 1750 for each level of the poison, the lesser being 1) / 100 per cent. A cure that works tells the target and the caster, one that fails tells the caster |
| Harm | nightshade, spider silk | At once, 1 to 15 damage, three quarters of it when resisted, scaled as the other damage spells, whole at any distance |
| Protection | garlic, ginseng, sulfurous ash | Adds a tenth of the caster's Magery points to the armor of the target, for 1.2 seconds a point of it |
| Magic Trap, Magic Untrap | | Disabled, see below |

A buff of a stat that is as strong or stronger than the new one stays; a weaker one is replaced. A buff and a curse of the
same stat add up. Protection is refused, before anything is spent, for a target that has it. It adds to what absorbs a
blow, as the armor of a piece does, and is the classic spell of the days before the defensive spells changed (it does not
stop a cast from being disturbed).

## The third circle

| Spell | Reagents | What it does |
| --- | --- | --- |
| Bless | garlic, mandrake root | As Agility, on the strength, the dexterity and the intelligence at once |
| Fireball | black pearl | A ball of fire flies to the target and, half a second later, does 10 to 16 damage, scaled as Magic Arrow |
| Poison | nightshade | Poisons the target unless it resists. The level goes by Magery and Poisoning together, less 10 for each tile beyond three: over 199.8 the deadly poison one time in ten and else the greater, over 170.2 the greater, over 130.2 the regular and else the lesser |
| Teleport | blood moss, mandrake root | The caster stands at the place picked, in sight, with a puff at both places. Refused before anything is spent when the caster is too loaded to move, nothing can stand there, a mobile stands there or an impassable item such as a shut door lies there, or a region forbids a teleport out of its place or into the destination |
| Telekinesis | blood moss, mandrake root | Uses an item from afar as a double click would: a container opens, a door swings. Refused for what has no use to make |
| Wall of Stone | blood moss, garlic | Three pieces of wall across the way from the caster to the place, which block movement for ten seconds; no piece where a mobile stands, where the caster cannot see or where an impassable item already lies. Refused in a guarded town |
| Magic Lock, Unlock | | Disabled, see below |

## The fourth circle

| Spell | Reagents | What it does |
| --- | --- | --- |
| Arch Cure | garlic, ginseng, mandrake root | Cure on everyone alive within two tiles of the place picked, with a chance a little lower, one per cent less |
| Arch Protection | garlic, ginseng, mandrake root, sulfurous ash | Protection on everyone alive within three tiles of the place picked who has none |
| Curse | nightshade, garlic, sulfurous ash | Lowers the three stats of the target together, as Clumsy, Feeblemind and Weaken do each |
| Fire Field | black pearl, spider silk, sulfurous ash | Five pieces of fire across the way, for 20 seconds: whoever steps onto one or stands in it burns for 2 damage once a second (1 when a try of Resisting Spells succeeds); the fire does not block. No piece where the caster cannot see or an impassable item lies. Refused in a guarded town |
| Greater Heal | garlic, ginseng, mandrake root, spider silk | Four tenths of the Magery and 1 to 10 hit points, with the refusals of Heal |
| Lightning | mandrake root, sulfurous ash | At once, a bolt for 12 to 20 damage, scaled as Fireball |
| Mana Drain | black pearl, mandrake root, spider silk | Takes 1 to 100 mana of the target (at most what it has) unless it resists, which it does 99 times in a hundred whatever its skill |
| Recall | black pearl, blood moss, mandrake root | The caster is carried to the place a rune is marked with, with its sound at both ends |

The fields are items with a time: Wall of Stone and Fire Field leave `magic_wall_of_stone` and `magic_fire_field_*` items on
the ground, which a script ends when the time is up (they are kept with the world and end after a restart too). The caster
of a fire is the aggressor of whoever burns, as for a blow: an innocent that burns makes it a criminal, an NPC fights
back, and an invulnerable or a dead one is left alone.

### Recall and runes

A recall rune is the item `recall_rune`; marked, it holds a place (`rune.x`, `rune.y`, `rune.z`, `rune.map`). Staff mark
one with [`.mark_rune`](commands/mark_rune.md) at the place where they stand; the Mark spell, of the sixth circle, will do
it for players. Recall is refused, before anything is spent, for what is not a rune, a rune not marked, a criminal, a
caster too loaded to move, a rune of another map, a place nothing can stand on or a mobile or an impassable item fills, and
a region that does not let a recall out of its place or into the destination (the `recall_out` and `recall_in` flags of the
regions).

### Region rules

`teleport_in`, `teleport_out`, `recall_in` and `recall_out` of [the regions](data-files/regions.md) are read: a travel is
refused when any region covering the place switches the rule off, not only the one that applies there. Scripts ask with
`world.travel_allowed`.

### Left disabled

Magic Lock, Unlock, Magic Trap and Magic Untrap have no script, and casting them says the spell is disabled: the only locks
of the game are those of doors (read by the door script, opened by keys and lockpicks), and a container has neither a lock
nor a trap to act on. They are built with the locks and traps of containers.

### Simplified

- Protection and Arch Protection add armor, as the classic spell did; the later rule that the protected is not disturbed by
  damage, and the penalty it came with, are not built.
- Recall does not check a fight in progress (the classic game refuses it for a while after a blow struck at a player; the
  engine has no such combat heat yet), and a mobile's pets do not follow.
- A prisoner is refused every spell, which also stops a travel out of the cells; a gate or a ride is not asked about a
  sentence.

### Which era

Where the classic game changed with its expansions, the spells follow the earliest, the days with no expansion, as the
casting tables of the first circle do: Harm hurts whole at any distance (the falloff came with the Second Dawn),
Protection and Arch Protection add armor, Reactive Armor sends back a share of a melee blow by the Magery of its wearer,
and Fire Field lasts 20 seconds whatever the Magery. The later rules (an absorbing Reactive Armor, a Protection that guards
against disturbance, a longer Fire Field) are not built.

## Try it

`.add test_kit_magery` gives a bag that fills when first opened with a full spellbook, 20 of each reagent, three
scrolls of each spell of the first four circles that is built, and two recall runes. Set the skill apart with
`.set skill magery 100`, and mark a rune with `.mark_rune`.

## Change the rules

- The spells are [`data/spells.toml`](data-files/spells.md): circle, words, reagents, target, flags, sound and graphics,
  and the scroll of each. Generated from UOX3 with `moongate-convert uox-spells`.
- A spell is `scripts/spells/<key>.lua` over `scripts/common/magic.lua`: `check` may refuse before anything is spent,
  `cast` is the effect. The mana, the delay and the Magery window come from the circle, in the server.
- The skill window is the Magery between which a try grows from a sure fail to a sure success; the scroll's is two circles
  easier.
- The scrolls are the templates with `script_id = "spell_scroll"` and the book the one with `script_id = "spellbook"`.
- Scripts reach the spells with the [`spell` module](https://moongate.sh/lua/spell/).

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/spells.toml`,
`scripts/spells/`, `scripts/common/magic.lua`, `scripts/common/field.lua`, `scripts/items/spellbook.lua`,
`scripts/items/spell_scroll.lua`, `scripts/items/magic_field.lua`, `scripts/items/test_kit.lua`,
`templates/items/magic/misc_magic.toml`, `templates/items/magic/scrolls.toml`, `templates/items/magic/fields.toml` and
`templates/items/test_kits.toml`, and the new messages of `data/messages`. A book already made keeps what it holds; a new
`spellbook` one is empty.

## Not yet

Circles 5 to 8, Magic Lock, Unlock, Magic Trap and Magic Untrap, Magic Reflection, inscription (writing scrolls), wands,
an NPC that casts, clearing the hands on a cast.

## See also

- [Combat](combat.md)
- [Potions](potions.md)
- [Skills](skills.md)
- [`.mark_rune`](commands/mark_rune.md)
- [spells.toml](data-files/spells.md)
