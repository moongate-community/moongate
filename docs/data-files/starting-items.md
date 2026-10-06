# Starting items

`starting_items.toml` holds the items a new character gets. The shipped file is written
from UOX3's `newbie.dfn`, with shard-specific entries such as the welcome letter.
[`mgctl convert uox`](../uox3-migration.md#starting-items) can regenerate its UOX3 entries.
`IStartingItemsService.GiveAsync` applies it to a new character.

A character gets every `[[set]]` with `common = true`, plus every set whose filters it
matches.

```toml
[[set]]
skill = "alchemy"
[[set.items]]
items = ["0x0f7a_black_pearl"]
amount = 3
equip = false

[[set.items]]
items = ["0x1f03_robe"]
hue = 1226
equip = true
```

| Field | Meaning |
| --- | --- |
| `common` | `true` gives the set to every character, whatever the filters |
| `skill` | Given to characters starting with this skill among their best ones |
| `race` | `human`, `elf` or `gargoyle`; unset is every race |
| `gender` | `male` or `female`; unset is both |
| `items` | Item template ids; one is picked at random |
| `amount` | How many, as dice; unset is 1 |
| `hue` | The hue to give the item; unset keeps its own |
| `equip` | `true` puts the item on the character, `false` in the backpack |
| `book_template` | Optional id from `templates/books/<id>.toml`; writes its text on the created item |
| `book_values` | Inline table of the document's declared custom values; strings, finite numbers or bools |
| `newbie` | `false` makes the item drop on death; unset or `true` makes it `Newbied` (kept) |

`GiveAsync` works in one transaction on the world database; if anything fails, the
character gets nothing:

1. It takes the character's `ultima.starting_items.best_skills` highest skills (default 3; ties
   go to the lower skill id; skills at 0 do not count).
2. It applies, in order, the sets of those skills, the common sets, then the sets of
   the character's race and gender.
3. It creates the backpack (`ultima.items.backpack_template`) and puts it on the
   `Backpack` layer.
4. For each entry it picks one item. A stackable item gets the whole `amount`;
   any other item is created `amount` times.
5. `equip = true` wears the item on its layer: the template's `layer`, or else the
   client's tiledata layer when tiledata marks the graphic wearable. If there is no layer, or the layer is taken, the item goes
   in the backpack instead, so the first item on a layer wins.
6. Worn shirts and robes take the shirt hue picked at creation, pants and skirts the
   pants hue; a hue of 0 keeps the item's own.
7. Items in the backpack go to a random spot inside its `containers.toml` bounds.

The starting gold is an ordinary entry: the shipped file gives 1000 coins with an
entry of the common set. Change its `amount` (at most 65535) to give more or less,
or remove the entry to give none. A root prepared before this change keeps its own
`starting_items.toml`, which `mgctl` does not overwrite: add this entry to its common
set, or new characters start without gold:

```toml
[[set]]
common = true
[[set.items]]
items = ["0x0eed_gold_coin"]
amount = 1000
equip = false
```

Food and drink are ordinary entries too: the common set of the shipped file gives three loaves of
bread and a pitcher of water, so a new character has something against
[hunger and thirst](../server-configuration.md). Neither they nor the gold are part of UOX3's
`newbie.dfn`: `mgctl convert uox` adds the three entries to the common set by itself, for the items
the source has.

```toml
[[set.items]]
items = ["0x103b_bread_loaf"]
amount = 3
equip = false

[[set.items]]
items = ["0x1f9e_pitcher_of_water"]
equip = false
```

## The blank book

The shipped common set also gives every new character a blank book it writes in, as ModernUO
does: twenty pages, with the character's name as its author. Its source is
[`templates/books/blank_book.toml`](books.md#books-a-player-writes-in). A root that keeps its own
`starting_items.toml` adds it to its common set:

```toml
[[set.items]]
items = ["readable_book"]
equip = false
book_template = "blank_book"
```

## Personalized starting letters

The shipped common set gives every new character a welcome letter in the backpack.
Its source is [`templates/books/welcome_letter.toml`](books.md), and its recipient
name is the new character's name, even before that character enters the world.

Add this entry **inside your existing common set**, alongside its other
`[[set.items]]` entries:

```toml
[[set.items]]
items = ["readable_scroll"]
equip = false
book_template = "welcome_letter"
book_values = { contact_name = "Vega" }
```

Use `book_template` without the `.toml` suffix. Supply every declared custom
variable, with no extra keys. Numbers use invariant formatting, bools become
`true` or `false`, and inserted strings are literal: `$player_name` inside a
custom value is not expanded again. Built-in values come from the creation
context; do not put them in `book_values`.

Title, author and body use `[localization].language` and are saved as
`book.template`, `book.title`, `book.author` and `book.content` before the item is
persisted. The displayed name follows the rendered title. Trading the letter,
renaming its recipient or editing the source cannot change its saved text.
`amount` creates separate, nonstacking copies; normal hue, newbie and backpack
placement rules still apply.

A source's [attachments](books.md#letter-attachments) are frozen separately for
every physical starting letter and saved before its first item write. Rewards
remain inside the saved entitlement until the bearer claims them, so they add no
starting weight. Any late attachment-preparation failure rolls back the character
and all earlier starting items. The shipped welcome source contains no rewards;
add them to your own source to opt in.

All item ids in the entry must explicitly resolve to `stackable = false` and
use `script_id = "readable_scroll"` or `"jail_note"`. Text entries must have
`equip = false`. If rendering fails during character creation, the character,
backpack and all starting items are rolled back in the same transaction.

An existing root keeps its edited `data/starting_items.toml` when you run
`mgctl init`: add the entry above yourself and restart the server. Removing it
disables the letter. `mgctl convert uox` regenerates starting items without this
shard-specific binding; add it again after converting.

## Validation at startup

The server stops when:

- `starting_items.toml` does not exist;
- a set has no items, or is not common and has no skill, race or gender;
- an entry has no items, names an item that is not an item template, or has an
  `amount` that can roll below 1 or above 65535;
- `ultima.items.backpack_template` or `ultima.items.gold_template` is not an item template;
- the gold template does not stack;
- a text entry references an unknown document, is equipped, uses an unsuitable
  item, supplies invalid/missing/extra values or cannot render valid text;
- an entry supplies `book_values` without `book_template`.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
