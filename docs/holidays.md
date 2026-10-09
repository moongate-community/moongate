# Holidays

The seasonal events of the [schedule](schedule.md) that come with content. Staff switches one with
[`.event`](commands/event.md); the dates are in `data/schedule.toml`.

| Event | Dates | What it does |
| --- | --- | --- |
| `halloween` | October 24 to November 15 | [Trick or treat](#halloween-trick-or-treat) |
| `christmas` | December 24 to January 1 | [Snowballs and gifts](#christmas-snowballs-and-gifts) |

## Halloween: trick or treat

While `halloween` is on, a player who says `trick or treat` within 4 tiles of a shopkeeper gets an
answer from it:

- Nine times out of ten the shopkeeper says a line, puts a candy in the backpack of the player (one of
  `lollipops`, `wrapped candy`, `jelly beans`, `taffy`, `nougat swirl`) and the player reads
  `You receive some candy.`
- One time out of ten it shouts `TRICK!` and blood splashes around the player.
- When several shopkeepers hear the same saying, only the first answers.
- After answering, a shopkeeper rests for 5 to 10 minutes. Asked sooner, it does not answer, and
  the player reads `That doesn't appear to have any more candy.` Each shopkeeper rests on its own.

The candies are food: double clicking one eats it. They are not sold. When the event starts and
ends, everybody is told.

The words are English whatever the language of the server; the lines and messages are in the
[language of the server](localization.md). Outside the event nothing happens.

### Files

| File | Content |
| --- | --- |
| `templates/items/misc/halloween.toml` | The candy templates (`lollipop1` to `lollipop3`, `wrappedcandy`, `jellybeans`, `taffy`, `nougatswirl`), converted from UOX3. |
| `scripts/common/trick_or_treat.lua` | The game; `shopkeeper.lua` calls it from `on_speech`. |
| `scripts/events/halloween.lua` | The `on_start` and `on_end` hooks: the announcements. |
| `data/schedule.toml` | The `[[event]]` with the dates; change `from` and `to` to move the season. |

### Ideas from ModernUO that are not here

The special treats for a begging master, the solid colour and the naughty twin tricks, the pumpkin
patch, the player zombies and the masks.

## Christmas: snowballs and gifts

**The gift.** A character that logs in while `christmas` is on finds in its backpack a pile of snow,
a pile of glacial snow, the light of the winter solstice and one decoration (a decorative topiary 60
times out of 100, a festive cactus 24, a snowy tree 16), and reads
`Happy Holidays! Gift items have been placed in your backpack.` The gift comes once a season: a character that got one less than 200
days ago gets none, and so does one whose backpack cannot take the piles (it is asked again at the
next login). The start and the end of the season are announced to everybody.

**The snowball.** Double clicking a pile that is in the backpack packs a snowball and opens a
cursor. The target must be a mobile within 10 tiles that carries a pile of snow too (it can throw
one back). The snowball flies to it with the sound and the gesture of the throw; both read the
client's own text, `You have just been hit by a snowball!` and
`You throw the snowball and hit the target!`. A player waits 5 seconds between two snowballs and cannot throw one while mounted, at
itself, or at something that carries no snow.

| File | Content |
| --- | --- |
| `templates/items/misc/winter_gifts.toml` | `snow_pile` and `glacial_snow`, the two piles with the snow script. |
| `scripts/items/snow_pile.lua` | The snowball. |
| `scripts/events/christmas.lua` | `on_start`, `on_end` and `on_login`: the announcements and the gift. |

The decorations come from `templates/items/misc/christmas.toml`. The mistletoe deed, the other
winter 2010 pieces and the snow of the safe zones are not here, and neither are the decorations of
the towns: they come later.
