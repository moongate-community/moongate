# Context menus

Click a mobile or an item and the client shows its context menu: a short list of what can be done with
it. Each entry is a text of the client, so every player reads it in its own language. Moongate fills
the menu with a few entries of its own and with the ones the [Lua script](scripting.md) of that NPC or
item adds.

How the menu opens is the client's own choice, a click or a click with a key held, as its options
say: the server only answers when the client asks.

## The server's entries

| On | Entry | From | Does |
| --- | --- | --- | --- |
| A mobile with a human body, a ghost too | `Open Paperdoll` | 18 tiles | What a double click on it does |
| Yourself, alive, with a backpack | `Open Backpack` | 18 tiles | What a double click on your backpack does |

They come first in the menu, and a script cannot take them out.

A ghost gets these and nothing else: the dead are offered no entry of a script, as nobody answers
their words.

## Entries of a script

A [mobile script](scripting/mobile-scripts.md) or an [item script](scripting/item-scripts.md) adds its
entries with two functions:

```lua
-- The entries this NPC adds for that player; nothing, or an empty table, for none.
function banker.on_context_menu(serial, player)
    return {
        { id = "bank", cliloc = 3006105, range = 12 },
    }
end

-- The player chose one of them: id is the entry's own.
function banker.on_context_menu_select(serial, player, id)
    if id == "bank" then
        bank.open(player)
    end
end
```

| Field of an entry | |
| --- | --- |
| `id` | The entry's own name in the script, given back at the choice. Required |
| `cliloc` | The number of the text of the client the entry shows, such as 3006105 `Open Bank Box`. It is the whole number: ModernUO and ServUO write these texts without the 3000000 (6105), and a number written so shows `MegaCliloc missing` in the client. Required |
| `range` | From how many tiles it can be chosen, 0 to 18; 18 when unset |
| `enabled` | `false` shows the entry greyed out; `true` when unset |

- `on_context_menu` answers at once: a script that calls `wait` there adds nothing.
- An entry that is not well formed (no `id`, a `cliloc` that is not a whole number above 0, a `range`
  outside 0 to 18) is left out, and the log says which script gave it.
- A menu holds 20 entries; more are dropped, with a warning in the log.
- `on_context_menu_select` may wait. It is called only for an entry of the menu the player was shown,
  that was not greyed out, while the player is within its range and still sees the target.

## What is checked

The client asks with the serial of what was clicked, and later says which entry was chosen. Moongate
keeps the last menu it sent to each player and checks the choice against it, as ModernUO does, so a
client cannot choose what it was not offered.

| When | Checked |
| --- | --- |
| The client asks | The target exists, on the player's map, within the view range (18 tiles), and the player may see and reach it: no menu for a hidden mobile or a staff-only item, for an item another mobile carries, for one in the player's bank while the bank is closed, nor for one lifted onto a cursor. A target with no entry gets no menu |
| An entry is out of its range, or not enabled | It is shown greyed out |
| The player chooses | The menu is the last one sent and for that same target; the index is one of its entries; the entry is not greyed out; the player is in its range now; the target is still there to see. The menu is good for one choice |

A choice that fails any of these does nothing, and the player is told nothing.

## The Enhanced Client's icons

The Enhanced Client can choose an entry from an icon of its own, such as the bank on a banker's
status bar, without showing the menu's list. It then names the entry by a fixed number instead of
its place in the menu: 0x78 for `Open Bank Box`, 0x12D for `Tame`, 0x82 to 0x89 for the commands of
a pet, and so on, as in ServUO. Moongate reads such a number as the entry of the menu it sent that
shows that text, and checks the choice as any other: the icon works only for an entry the menu
really has, that is not greyed out and is in range. Scripts need do nothing: an entry with the
cliloc 3006105 is the one the bank icon chooses.

## Not built yet

- The entries of systems still to come: buy and sell on a vendor, the stable, the commands of a pet,
  taming, the party.
- The old menus of item pictures and of questions (packet 0x7C).

## See also

- [Bank](bank.md): the banker's `Open Bank Box`
- [Mobile scripts](scripting/mobile-scripts.md) and [item scripts](scripting/item-scripts.md)
