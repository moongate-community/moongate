# Vendors

An NPC vendor sells goods for gold, as in ModernUO. A player opens the shop window of a vendor, sees what it sells
with the price of a piece, picks what to buy and gets the goods in the backpack. What each vendor sells is a
[shop](data-files/shops.md), converted from ModernUO's `SBInfo` classes. A player can also sell to a vendor. The
restock of the shelves is not built yet.

## Open the window

A player opens the window in one of two ways, from as far as the vendor can be reached:

- Click the vendor and pick *Buy* or *Sell* in the [context menu](context-menus.md), within 8 tiles.
- Say *vendor buy* or *vendor sell* within 4 tiles. The client turns the words into a speech keyword in any language of the client,
  so *vendor buy* works in an Italian client as well. When several vendors hear the words, one answers.

A template that inherits `basevendor` runs the script, but only those with a shop sell: the others do nothing when
*Buy* is picked. A vendor opens the window only when it has a [shop](data-files/shops.md) with goods in stock, is in the world, is no
more than 10 tiles away, is in sight, and the player is alive. A murderer in a guarded place is refused by the vendor's
voice (cliloc 501522).

## Buy

The window lists the goods with their prices. The player chooses a number of pieces for each line and confirms. A
purchase is everything or nothing: when a check fails, nothing is taken and nothing is given.

- A purchase has 1 to 100 lines. A line chosen twice adds up. Each line is a line of the window, and its amount is
  at most the vendor's stock.
- The total is the price of a piece times the pieces of every line; a total over `int.MaxValue` is refused. A game
  master pays nothing.
- The gold comes from the backpack and the bags inside it. When the backpack lacks it and the total is 2000 or more,
  the bank makes up the difference; under 2000 the vendor says *thou canst not afford* (cliloc 500192) and the bank
  is left alone. A bank that cannot make up the difference is answered by cliloc 500191. The check comes before any
  item is made, so a refused purchase costs nothing.
- A stackable line gives one stack, any other line gives one item for each piece. The serials for all of them are
  reserved before anything is taken; an order that needs more than the server has ready is refused with cliloc
  500187.
- The goods go to the backpack. When they do not fit, they are put on the ground at the player's feet.
- The player reads what was paid: cliloc 1151639 for gold of the backpack, 1151638 when the bank was used.

The window closes after every answer, a purchase or a refusal. A reply that is not for the open window of that
vendor, or that has more than 100 lines, is dropped.

## Sell

*Sell* opens a list of what the player carries that the vendor's shop buys, with the gold paid for one piece. The list has
the items of the backpack and of the bags in it, 250 at most. An item that is worn, held on the cursor, not movable or a
container with something inside is not offered. A vendor that buys nothing the player carries says *you have nothing I
would be interested in*.

The player chooses items and how many pieces of each, and confirms. A sale is everything or nothing, like a purchase:

- The reply has fewer than 100 lines. Each item must be one that was offered and is still in the backpack, and a line
  chosen twice adds up. An amount over what the item holds is cut to it. The
  total is refused over `int.MaxValue`. Items that are not regular loot, such as the starting items of a new character,
  are not bought.
- The vendor must be in reach and the player alive, and a murderer in a guarded place is refused, as for a purchase.
- The gold is paid in piles of 60000 in the backpack, or in the bank box when the backpack has no room for them. When
  neither has the room, nothing is sold and the player is told the backpack is full.
- The pieces are taken from the items, and an item sold whole is deleted. The vendor does not keep them on its shelves
  yet.

## Stock

Each vendor starts with the amount of each line of its shop. A purchase lowers it, and the next window shows what is
left. The stock is kept in memory only: a restart gives every vendor its full shelves again, as in ModernUO. Restock
over time is not built yet.

## What the client receives

The window is the client's own, driven by these packets: `0x2E` puts two virtual containers on the vendor (layers
*ShopBuy* and *ShopResale*, which the client needs or it crashes), `0x3C` fills the first with one virtual item for
each line, `0x74` gives the price and the name of each line, and `0x24` opens the window with gump `0x30`. The
purchase comes back in `0x3B`, and the server answers it with a `0x3B` that closes the window. The serials of the
virtual items count down from `Serial.MaxVirtual` and mean nothing outside the open window. The sell list goes out in `0x9E` and the player's choice comes back in `0x9F`;
its items are the player's own, with their real serials. See [Packets](packets.md).

## For script authors

The vendor templates that inherit `basevendor` run `scripts/mobiles/shopkeeper.lua`, which offers *Buy* in the context
menu and listens for *vendor buy*. The script is called `shopkeeper` because `vendor` is the name of the Lua module
that opens the window:

```lua
vendor.open_buy(npc, player)
```

A script of a vendor that has its own, such as `banker.lua` or `healer.lua`, can call it too. See
[Lua modules](lua-modules.md).

## Limits

- Buy-back and the restock of the shelves are the next slice.
- Skill trainers, pets, bulk order deeds, the price scalar of towns, player vendors and the gold a vendor holds are
  not built.
- The shops shipped with the server hold the lines of ModernUO that have an item template. The converter reports the
  others: weapons and a few other goods have no template yet, so those vendors sell less than in ModernUO.
