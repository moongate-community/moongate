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

Each scroll takes the reagents of its spell, one blank scroll and the mana of its circle. The mana is paid once, by a try
that is made: a success pays it, and so does a failure ("You fail to inscribe the scroll, and the scroll is ruined."),
which also loses half of the materials, rounded down and at least one unit of the first reagent. A success says "You
inscribe the spell and put the scroll in your backpack." A scroll is never exceptional nor marked.

| Circle | Mana | Inscription window |
| --- | --- | --- |
| First | 4 | -25 to 25 |
| Second | 6 | -10.8 to 39.2 |
| Third | 9 | 3.5 to 53.5 |
| Fourth | 11 | 17.8 to 67.8 |
| Fifth | 14 | 32.1 to 82.1 |
| Sixth | 20 | 46.4 to 96.4 |
| Seventh | 40 | 60.7 to 110.7 |
| Eighth | 50 | 75 to 125 |

The chance is one in two at the least of the window and sure at its end, as in every craft; a negative least, as the
first circle has, is always tried, so a beginner writes first circle scrolls at once. The recipes are the 64 spells,
listed whether or not the book has them, so a player finds what there is to learn; each takes the reagents the spell
asks for when cast.

## Change the rules

- The recipes are [`data/crafts/inscription.toml`](data-files/crafts.md), written by the converter from
  `data/spells.toml` and the table of circles; `spell` and `mana` are the two fields only this craft uses.
- The tool is the template with `script_id = "inscription_tool"` (`scripts/items/inscription_tool.lua`): the pen and
  ink, whose graphics are `0x0fc0_pen_and_ink` and `0x0fbf`. The mapmaker's pen stays a tool of
  [cartography](cartography.md).
- The failure and success texts and the sound of a craft are the tables `FAILED_TEXT`, `SUCCESS_TEXT` and
  `SUCCESS_SOUND` of `scripts/common/crafting.lua`.
- The staff bag `.add test_kit_inscription` holds a pen and ink, 100 blank scrolls, the reagents and a full
  spellbook; set the skills apart with `.set skill inscription 100` and `.set skill magery 100`.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/inscription.toml`,
`data/crafts/resources.toml` (the list `blank_scrolls`), `scripts/common/crafting.lua`, `scripts/gumps/craft_menu.lua`,
`scripts/items/inscription_tool.lua`, `scripts/items/test_kit.lua`, `templates/items/test_kits.toml` and
`templates/items/skills/tools/inscription.toml`. A pen and ink that was a cartographer's tool opens inscription now;
cartography is opened by the mapmaker's pen.

## Not yet

Scrolls of the later expansions, bulk order deeds and writing a blank spellbook. The scrolls of Magic Lock, Unlock,
Magic Trap and Magic Untrap can be written, but they cannot be cast until [Magery](magery.md#left-disabled) has them.

## See also

- [Magery](magery.md)
- [Cartography](cartography.md)
- [Carpentry](carpentry.md)
- [Crafts data files](data-files/crafts.md)
