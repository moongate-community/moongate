# Holidays

The seasonal events of the [schedule](schedule.md) that come with content. Staff switches one with
[`.event`](commands/event.md); the dates are in `data/schedule.toml`.

| Event | Dates | What it does |
| --- | --- | --- |
| `halloween` | October 24 to November 15 | [Trick or treat](#halloween-trick-or-treat) |

## Halloween: trick or treat

While `halloween` is on, a player who says `trick or treat` within 4 tiles of a shopkeeper gets an
answer from it:

- Nine times out of ten the shopkeeper says a line, puts a candy in the backpack of the player (one of
  `lollipops`, `wrapped candy`, `jelly beans`, `taffy`, `nougat swirl`) and the player reads
  `You receive some candy.`
- One time out of ten it shouts `TRICK!` and blood splashes around the player.
- After answering, a shopkeeper rests for 5 to 10 minutes. Asked sooner, it does not answer, and
  the player reads `That doesn't appear to have any more candy.` Each shopkeeper rests on its own.

The candies are food: double clicking one eats it. They are not sold. When the event starts and
ends, everybody is told.

The words are English whatever the language of the server; the lines and messages are in the
[language of the server](localization.md). Outside the event nothing happens.

### Files

| File | Content |
| --- | --- |
| `templates/items/food/halloween.toml` | The seven candy templates. |
| `scripts/common/trick_or_treat.lua` | The game; `shopkeeper.lua` calls it from `on_speech`. |
| `scripts/events/halloween.lua` | The `on_start` and `on_end` hooks: the announcements. |
| `data/schedule.toml` | The `[[event]]` with the dates; change `from` and `to` to move the season. |

### Ideas from ModernUO that are not here

The special treats for a begging master, the solid colour and the naughty twin tricks, the pumpkin
patch, the player zombies and the masks. Christmas and the decorations of both come later.
