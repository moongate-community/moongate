# Item scripts

This page is part of [Writing Lua scripts](../scripting.md). The functions a script calls on its item are in
the reference: [`item`](https://moongate.sh/lua/item/).

An item template names its script with `script_id`, the name of a global Lua table
defined by `scripts/items/<script_id>.lua`. The files of `scripts/items/` load at
startup like the [mobile scripts](mobile-scripts.md), and `script reload
items/potion.lua` reloads one.

```toml
# a potion template of your own
[[item]]
id = "my_potion"
item_id = 0x0F0C
script_id = "potion"
```

| Function | When |
| --- | --- |
| `on_use(serial, user)` | A player double clicks the item, carried (worn or in its containers) or on the ground within 2 tiles and in sight; farther, the player reads "That is too far away." and nothing runs. Items inside a container lying on the ground cannot be used yet: the player reads "That is too far away.". A missing `on_use`, or one that raises an error, lets the default action follow. Return `true` to stop the default action, such as opening a container; return nothing to let it follow. A handler that calls `wait` counts as handled; after the wait the item may have moved, so check it again, for example `item.owner(serial) == user`. |
| `on_move_over(serial, mobile)` | A player stepped onto the cell of the item, lying on the ground at the player's height, up to 14 above its feet, or below them and tall enough to reach them (ModernUO's rule). It runs after the step was acknowledged and shown to the players around; an NPC runs `on_npc_move_over` instead. Once a script moved the player off the cell, the other items of the cell are not run. Arriving by teleport does not trigger it, so two teleporters that point at each other do not loop. |
| `on_npc_move_over(serial, npc)` | An NPC stepped onto the cell of the item, by the same height rule. It runs on the turn of the game loop after the step, so the NPC may already have moved on: check where it is |
| `on_speech(serial, speaker, text, keywords, type)` | A player said `text` within hearing of the item, lying on the ground (commands are not heard): 15 cells aloud or as an emote, 1 cell for a whisper, 18 for a yell. `speaker` is the player's serial; `keywords` the speech keywords the client found, an array of numbers; `type` how it was said, such as `SpeechType.Whisper`. Every scripted ground item in range is asked, after the NPCs, so a script checks its own range and words. It may call `wait`. |
| `on_equip(serial, wearer)` | The item went onto a layer of the mobile `wearer`, dropped on the paperdoll. A worn item lifted and bounced back never left its layer, and items loaded or spawned already dressed raise nothing. It cannot refuse the item: `can_equip` does. |
| `on_unequip(serial, wearer)` | The item left the layer of `wearer`: dropped in a container or on the ground, or merged into a stack (the item is gone then, so `item.*` gives `nil`). Logging out, removing an NPC or deleting a mobile with its items raise nothing. |
| `on_pickup(serial, picker)` | The player `picker` lifts the item from a container, the paperdoll or the ground; lifting part of a stack lifts this item, and the rest left behind is not new. While it is held, `item.consume` and `item.delete` refuse it. A held item ends in `on_drop`, in `on_equip` when it is worn by a new wearer, or in nothing: when it bounces back, is worn again on the layer it came from, or its player logs out holding it. |
| `on_drop(serial, dropper)` | The player `dropper` puts the held item down: into a container, on the ground, or onto a stack (the item is gone then, so `item.*` gives `nil`). Not when it bounces back or is worn. A worn item put down runs `on_unequip` first, then `on_drop`. |
| `can_pick_up(serial, picker)` | The player `picker` is about to lift the item, from a container, the paperdoll or the ground, once every rule of the server allows it and before a stack is split. Return `false` to refuse: the item stays where it is and the client shows no message of its own. A worn item is lifted before it is taken off, so this is also where a script keeps an item on its wearer |
| `can_drop(serial, dropper)` | The player `dropper` is about to put the held item down, anywhere: on the ground, into a container or onto a stack. Return `false` to refuse: the item goes back where it was lifted from |
| `can_equip(serial, wearer)` | The held item is about to be worn by `wearer`, once the layer is free and the rules allow it. Return `false` to refuse: the item goes back where it was lifted from and `on_equip` does not run. Not asked of a worn item lifted and put back on its layer, which it never left |
| `can_insert(serial, mobile, item)` | Asked of a container: the player `mobile` is about to put `item` into it, or onto a stack that lies directly in it, once the rules allow the drop. `serial` is the container, carried or lying on the ground; a container holding that container is not asked. Return `false` to refuse: the item goes back where it was lifted from. It is asked after the item's own `can_drop`, and once for each drop |
| `on_context_menu(serial, player)` | That player asks for the item's [context menu](../context-menus.md): an item it carries, or one on the ground or in a container there, in view. Return the entries the item adds, a table of `{ id, cliloc, range, enabled }`, or nothing; it answers at once and must not `wait`. |
| `on_context_menu_select(serial, player, id)` | The player chose one of those entries: `id` is the entry's own. Called only for an entry the player was shown and is in range of. |
| `on_timer(serial, name)` | A timer of the item, started with `item.start_timer`, is due. Timers are checked once a second. The timer is gone when the function runs: start it again there for something that repeats. One that came due while the server was down, or while the character carrying the item was offline, runs as soon as the item is in the world again. It may call `wait` |
| `on_darkness(serial, dark)` | Every 30 seconds, and right after `.globallight`, on a lamp post (template `decoration_light`, prop `decoration_type` LampPost1 to LampPost3) whose spot turned dark (`true`) or light (`false`) |
| `on_create(serial)` | A newly created item enters the world: the equipment, backpack and loot of a spawned NPC, before that NPC's `on_spawn`, and a chest a spawn region makes, with everything inside it. A new character's starting items, the rest of a split stack, and items made by `item.give`, `item.create`, `item.add_loot` or `.decorate` raise nothing. |

`on_equip`, `on_unequip`, `on_pickup`, `on_drop` and `on_create` run right after what
caused them, on the next turn of the game loop, once the players have seen it: a script
may then delete or consume the item. They are notifications: none can refuse the move.

`can_pick_up`, `can_drop`, `can_equip` and `can_insert` are questions, asked before the
move and answered at once: only `false` refuses. A missing function, an error, a call to
`wait` or any other value lets the move follow, so a broken script never locks an item. Tell
the player why with `mobile.message` before returning `false`. They are asked for the moves
a player makes with the client, staff included; a script that moves an item itself
(`item.move_to`, `item.move_into`) is not asked. While a question is asked the item counts as held, so
`item.delete`, `item.consume`, `item.move_into` and the functions that change it refuse it:
answer the question there, and act on the item in `on_pickup`, `on_drop` or `on_equip`.

```lua
-- a cursed ring: once worn, it stays on
function ring.can_pick_up(serial, picker)
    -- worn: it has an owner and lies in no container
    if item.owner(serial) == picker and item.container(serial) == nil then
        mobile.message(picker, "The ring will not come off.")
        return false
    end
end
```

The script acts on its item with the `item` module, passing its serial; `user` is
the serial of the player. The distribution's `scripts/items/potion.lua`, copied into the root by `mgctl`; no
template uses it yet:

```lua
potion = {}

function potion.on_use(serial, user)
    item.message(serial, user, "You drink the potion.")
    item.consume(serial)

    return true
end
```

The scripts the distribution ships for doors, lights, food, teleporters, moongates, clocks and containers
are described in [Shipped scripts](shipped-scripts.md).
