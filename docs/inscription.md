# Inscription

A scribe writes the scroll of a spell with a pen and ink, on a blank scroll, from the reagents of the spell. It is the
last craft: the rules are the ones of every craft (see [Carpentry](carpentry.md)): the chance, the failures that lose
materials, a pen that wears out, and Make last. What is new is that a recipe asks for a spell and for mana, as a cast
does. The scrolls made are the ones a mage [casts from or writes into a book](magery.md).

## How to write a scroll

1. Carry a pen and ink, blank scrolls and the reagents in your backpack, and a spellbook that holds the spell (worn, or
   in the backpack but not in a bag inside it).
2. Double click the pen and ink. The crafting gump of inscription opens, with a group for each circle, First to Eighth.
3. Press the button before a recipe, or open its page, which tells the reagents, the Inscription window, the mana and
   your chance.

A recipe is refused, before anything is spent, with the classic text:

| Text | When |
| --- | --- |
| You don't have that spell! | no book you carry holds the spell |
| You don't have the components needed to make that. | a reagent or the blank scroll is missing |
| Insufficient mana for this spell. | you have less mana than the circle costs |
| You don't have the required skills to attempt this item. | Inscription is below the least of the recipe |

All of it is checked when you start and again at the second stroke, so a book given away or mana spent in between
refuses the second stroke and takes nothing.

## What it takes

Each scroll takes the reagents of its spell, one blank scroll and the mana of its circle. The mana is paid once, by a
success only: the scroll is made, the reagents and the blank scroll go with it, and so does the mana. A failure ("You
fail to inscribe the scroll, and the scroll is ruined.") takes no mana and ruins one unit of every resource, the blank
scroll too, which is not the half of the materials the other crafts lose. A success says "You inscribe the spell and put
the scroll in your backpack"; when the backpack is full the scroll lies at your feet and only that is said. A scroll is
never exceptional nor marked.

| Circle | Mana | Inscription window |
| --- | --- | --- |
| First | 4 | 0 to 40.1 |
| Second | 6 | 6.1 to 50.1 |
| Third | 9 | 16.1 to 60.1 |
| Fourth | 11 | 26.1 to 70.1 |
| Fifth | 14 | 36.1 to 80.1 |
| Sixth | 20 | 46.1 to 90.1 |
| Seventh | 40 | 66.1 to 110.1 |
| Eighth | 50 | 76.1 to 120.1 |

The windows are those of the classic data file the engine's rule was built for: the chance is one in two at the least
of the window and sure at its end, as in every craft. The first circle starts at 0 instead of 1.1, so a beginner with no
skill at all can still try, and gains. The recipes are the 64 spells, listed whether or not the book has them, so a
player finds what there is to learn; each takes the reagents the spell asks for when cast.

## Change the rules

- The recipes are [`data/crafts/inscription.toml`](data-files/crafts.md), written by the converter from
  `data/spells.toml` and the table of circles; `spell` and `mana` are the two fields only this craft uses.
- The tool is the template with `script_id = "inscription_tool"` (`scripts/items/inscription_tool.lua`): the pen and
  ink, whose graphics are `0x0fc0_pen_and_ink` and `0x0fbf`. The mapmaker's pen stays a tool of
  [cartography](cartography.md).
- The failure and success texts and the sound of a craft are the tables `FAILED_TEXT`, `SUCCESS_TEXT` and
  `SUCCESS_SOUND` of `scripts/common/crafting.lua`; the crafts whose failure ruins one unit of every resource are the
  table `FAIL_LOSES_ALL`.
- The staff bag `.add test_kit_inscription` holds a pen and ink, 100 blank scrolls, the reagents and a full
  spellbook; set the skills apart with `.set skill inscription 100` and `.set skill magery 100`.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/inscription.toml`,
`data/crafts/resources.toml` (the list `blank_scrolls`), `scripts/common/crafting.lua`, `scripts/gumps/craft_menu.lua`,
`scripts/items/inscription_tool.lua`, `scripts/items/test_kit.lua`, `templates/items/test_kits.toml` and
`templates/items/skills/tools/inscription.toml` and `templates/shops/mapmaker.toml` (the mapmaker sells the mapmaker's pen).
A pen and ink that was a cartographer's tool opens inscription now; cartography is opened by the mapmaker's pen.

## Not yet

Scrolls of the later expansions, bulk order deeds and writing a blank spellbook.

The scrolls of **Magic Lock, Unlock, Magic Trap and Magic Untrap can be written but not cast**: the recipes are there,
and a scribe can make and sell them, but casting them says the spell is disabled until containers have a lock and a
trap state (see [Magery](magery.md#left-disabled)).

## See also

- [Magery](magery.md)
- [Cartography](cartography.md)
- [Carpentry](carpentry.md)
- [Crafts data files](data-files/crafts.md)
