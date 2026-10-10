# Magery

A mage casts the spells of Magery from a spellbook or reads them from a scroll, by the rules of the classic game: words
of power, a delay in which the caster stands still, a target cursor, reagents, mana and a skill check. This first part
has the spellbook, the casting engine and the seven spells of the first circle; the other circles follow.

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
   Magery. A success pays the mana, uses up one scroll and runs the spell. A failure fizzles: the reagents are lost, the
   mana is not, a scroll stays.
5. Damage taken while the delay runs ruins a spell above the first circle: the caster is told, and waits
   the less of the delay was done, from a second down to a fifth of one. The first circle is never ruined, and a cast waiting for its
   target is not.

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

Reactive Armor has no script yet: casting it says the spell is disabled. A harmful spell makes the
caster the aggressor of its target: a criminal against an innocent who does not fight it, and an NPC fights back. A curse
of a stat that is as strong or stronger stays.

## Try it

`.add test_kit_magery` gives a bag that fills when first opened with a full spellbook, 20 of each reagent and three
scrolls of each first circle spell. Set the skill apart with `.set skill magery 100`.

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
`scripts/spells/`, `scripts/common/magic.lua`, `scripts/items/spellbook.lua`, `scripts/items/spell_scroll.lua`,
`scripts/items/test_kit.lua`, `templates/items/magic/misc_magic.toml`, `templates/items/magic/scrolls.toml` and
`templates/items/test_kits.toml`. A book already made keeps what it holds; a new `spellbook` one is empty.

## Not yet

Circles 2 to 8, Reactive Armor, inscription (writing scrolls), wands, an NPC that casts, the places where magic is
refused, clearing the hands on a cast, Magic Reflection.

## See also

- [Combat](combat.md)
- [Potions](potions.md)
- [Skills](skills.md)
- [spells.toml](data-files/spells.md)
