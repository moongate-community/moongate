# Readable text templates

Put one plain-text document in `<root>/templates/books/<name>.toml`. The file
`welcome_letter.toml` has id `welcome_letter`; subdirectories are allowed but
filename stems must be unique. On this machine the server root is `~/moongate`.

The server loads these sources at startup. A script creates a personalized
scroll with `book.give`, or inscribes an existing readable item with `book.write`.
Title, author and body are resolved once and saved on that individual item.
Trading it, reading it as another player, renaming the recipient, editing the
source or restarting the server never changes its saved text.

## A welcome letter

The shipped example is `templates/books/welcome_letter.toml`:

```toml
title = "Welcome $player_name"
author = "Lord British"
content = """
Dear $player_name,

Welcome to $server_name.
Bring this letter to $contact_name.
"""
variables = ["contact_name"]

[translations.ita]
title = "Benvenuto $player_name"
content = """
Caro $player_name,

Benvenuto a $server_name.
Porta questa lettera a $contact_name.
"""
```

Create it from a Lua script that has the player's serial:

```lua
local letter = book.give(player, "welcome_letter", { contact_name = "Vega" })
if letter then
    book.open(letter, player)
end
```

The `readable_scroll` item template uses graphic `0x14ED`, does not stack, and
runs `scripts/items/readable_scroll.lua`. A double click opens a parchment gump
with a scrollable body. Stored text is plain; HTML characters are escaped only
when displayed, and line breaks are preserved.

## Delivery at character creation

The shipped [starting items](starting-items.md#personalized-starting-letters)
common set delivers `welcome_letter` to each new character's backpack with
`contact_name = "Vega"`. Set `book_template` and `book_values` in a starting-item
entry to deliver any catalog document. Values and the new character's name are
resolved once, and text is persisted with the character and starting items in
one transaction. Existing roots must add the entry to their preserved
`data/starting_items.toml`.

## Fields

| Field | Meaning |
| --- | --- |
| `title` | Required, nonblank source title; supports variables |
| `author` | Optional, defaults to empty; supports variables |
| `content` | Required, nonblank multiline source body; supports variables |
| `variables` | Optional array of required custom value names |
| `item_template` | Existing item template; default `readable_scroll` |
| `attachments` | Optional reward entries; see [Letter attachments](#letter-attachments) |
| `translations.<language>` | Optional `title`, `author` and `content` overrides; each missing field falls back to the top-level value |

Ids and variable names use lowercase letters, digits and underscores, beginning
with a letter. Custom declarations cannot repeat or shadow built-ins. The
selected item template must explicitly set `stackable = false` and use
`script_id = "readable_scroll"` or `"jail_note"`; inherited values count.

The creation language is `[localization].language`. Supported overrides are
`eng`, `ita`, `fre`, `ger`, `spa`, `por`, `pol` and `cze`. An absent language
uses the top-level fields.

## Imported book texts

The shipped `templates/books/modernuo` catalog contains 62 static books imported from
ModernUO. For example, `book.give(player, "modernuo_grammar_of_orcish")` creates a readable
copy in the configured creation language. Every imported book retains the English source
and has title/body translations in Italian, French, German, Spanish, Portuguese, Polish
and Czech; author names remain unchanged. Reimport preserves existing translation fields.
[Import book texts](../book-content-import.md)
documents the converter, source comparison and reruns. These entries use the current
parchment interface.

## Letter attachments

Add attachments to a document source, outside any translation table:

```toml
[[attachments]]
item_template = "0x0eed_gold_coin"
amount = 100

[[attachments]]
item_template = "0x103b_bread_loaf"
amount = 3
newbie = false
```

Each entry requires an existing `item_template`. `amount` defaults to 1 and uses
the shared integer/dice syntax; its entire range must stay within 1..65535.
`hue` is optional and uses the shared fixed/range/list syntax; leaving it unset
keeps the item factory's hue. `newbie` defaults to false (ordinary loot); true
marks the delivered item as kept on death. The batch may contain at most 32
physical items, counting every possible nonstackable copy at the maximum roll.
Translations change only text and cannot add or replace attachments.

`book.give` and starting items freeze an independent batch for each physical
letter at creation, including rolled quantities, hue and factory properties.
The letter keeps that batch through source edits, trades and restarts. Reward
items enter the world and contribute weight only when claimed; until then only
the letter weighs. They are delivered as distinct stacks/items without merging
existing stacks.

A reader sees **Claim attachments** (**Ritira allegati** in Italian) only while
an unclaimed letter is in their own backpack, including nested bags. Trading
that letter transfers its unclaimed rewards. Ground letters, bank letters,
letters held on the cursor and letters in another player's inventory have no
claim action. Reading remains available under the normal access rules.

Claiming delivers the whole batch once, or nothing if the backpack lacks weight
capacity, item capacity or free grid slots. Make room and reopen the letter to
retry. On success the button disappears and the same letter remains readable.
Invalid saved rewards or an unavailable/incompatible item template refuse the
claim; editing the TOML does not recreate the issued batch.

`book.write` preserves an existing valid attachment payload, including after
claiming. It cannot add rewards to a plain letter or repair malformed rewards.
A refused rewrite leaves the text unchanged. A preparation failure makes live
`book.give` return nil before allocating the letter, and rolls back character
creation when the letter is a starting item.

The shipped welcome letter has no rewards. To opt in, add the entries above to
your own `templates/books/welcome_letter.toml` and restart. If your root already
has an edited starting-items file, bind that source using the
[personalized starting-letter entry](starting-items.md#personalized-starting-letters).
Only newly issued letters receive the configured batch. Mailboxes and native
book covers/pages/editing remain separate features.

## Variables

| Built-in | Creation-time value |
| --- | --- |
| `player_name` | Specified recipient's name |
| `server_name` | Server name, as in the MOTD |
| `realm_name` | Current realm name |
| `version`, `codename` | Running server version and codename |
| `users_online` | Connected sessions that have a character |

Both `$player_name` and `${player_name}` work. Braces separate a name from a
suffix: `${player_name}_letter`; `$player_name_letter` names a different
variable. `$$` prints one literal dollar; `$${player_name}` prints the literal
`${player_name}`.

A supplied value is a string, finite number formatted without locale separators,
or bool (`true` / `false`). Empty strings are allowed. Every declaration must
be supplied; missing or extra keys, tables, functions, nil or nonfinite numbers
fail. Inserted values are literal: a value containing `$server_name` is never
expanded again.

The shared formatter preserves the [MOTD](../motd.md) braced-only grammar and its
async plugin resolvers. Documents use explicit custom values.

## Lua operations and saved fields

| Call | Result |
| --- | --- |
| `book.give(player, template_id, values?)` | New item serial, or nil |
| `book.write(item, template_id, player, values?)` | True on success; false leaves previous fields unchanged |
| `book.open(item, player)` | True when opened or queued for the next loop turn; false on refusal |

Creation renders all fields before taking a serial or giving an item. It can
fail for an unknown template or recipient, invalid values, a missing backpack
or exhausted serial pool. Writing also rejects held items, stacks and unsupported
readable item templates.

Reading uses normal access: the reader's carried items, including an open bank,
or a nearby reachable ground item/container on the same map. Another player's
backpack, a closed bank, a held item/container or a distant ground root is refused.
When called from Lua, opening is queued; access, item existence and the original
connected session are checked again before sending.

Saved props are `book.template`, `book.title`, `book.author` and `book.content`.
The item's displayed name follows the rendered title.

## Validation and upgrades

A missing `templates/books` directory means an empty catalog. Invalid TOML,
duplicate ids, unknown/invalid tokens, duplicate declarations, unsupported
translations and unsuitable item templates stop startup with the source path.

Rendered title and author are each limited to 128 UTF-16 units. Source and
rendered content are limited to 16,384 units. NUL and control characters other
than line breaks and tabs are rejected. HTML escaping may make a valid body too
large for a client packet; reading then fails and logs a reason without
truncating the text.

Restart after editing a source. Only newly created or explicitly rewritten
documents use it. Run `mgctl init` after upgrading to copy missing shipped
files into your root; existing edited files are preserved. For an existing root,
complete the two edits below **before starting the upgraded server**.

### Existing roots

`mgctl init` adds the new book sources, scroll template and scroll script, but
preserves your existing `templates/items/jail.toml` and
`scripts/items/jail_note.lua`. It cannot merge these changes automatically.
Back up both files and merge these edits while retaining your custom fields,
comments and other functions:

1. In `templates/items/jail.toml`, add this field to the `[[item]]` with
   `id = "jail_release_note"` (or change its existing value):

```toml
stackable = false
```

2. In `scripts/items/jail_note.lua`, replace the existing `on_use` function
   with this delegate. Keep the `jail_note = {}` declaration and any other
   custom functions:

```lua
function jail_note.on_use(serial, user)
    book.open(serial, user)
    return true
end
```

Without the first edit, the new jail book source fails startup validation.
Without the second, the old script reads only `jail.text` and cannot display new
notes saved as `book.content`. The delegate handles both saved formats. Move any
custom release-note wording into `templates/books/jail_release_note.toml`;
already issued notes keep their saved text. Start the server after both merges.

## Jail notes and native books

`jail_release_note.toml` carries the existing wording in eight languages.
The jail supplies the days, cell, UTC dates, actual fine, staff name and optional
reason. Its `player_name` is the name recorded with the sentence. Old notes with
only `jail.text` remain readable, and `jail.cell`, `jail.days` and `jail.fine`
remain available.

Native book covers, pages, book packets and editing are the next slice. This
catalog and saved plain text provide their foundation.
