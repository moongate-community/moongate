# Item scripts

An item template's `ScriptId` names a Lua script that runs when players interact with the item. The
dot is a namespace separator, so `ScriptId: items.magic_torch` means
`<root>/scripts/items/magic_torch.lua`, and every shipped template that has a script uses the
`items.` namespace.

| Concern | Type |
|---|---|
| Runtime | `Moongate.Scripting.Items.LuaItemScriptRuntime` (`IItemScriptRuntime`) |
| Dispatcher | `Moongate.Server.Subscribers.ItemScriptSubscriber` |
| Hooks enum | `ItemScriptHookType` (`Moongate.Server.Abstractions.Types.Items`) |
| Seeder | `Moongate.Scripting.Items.ItemScriptAssetSeeder` |

This is the same shape as [NPC brains](ai.md): a file that returns a table whose `on_*` functions are
the hooks. Item scripts are simpler — no per-instance state, no tick, no binding.

## The shape of a script

```lua
local magic_torch = {
    id = "items.magic_torch",
}

function magic_torch.on_double_click(ctx)
    log.info("torch " .. ctx.item.serial .. " was double-clicked")
end

return magic_torch
```

The file **must return a table**; one that returns anything else is ignored with a warning. Every
hook is optional — define only what you need, and an item whose script defines none simply does
nothing.

`ScriptId: none`, which every shipped item template carries, means no script. So does an empty
value. Neither reaches the disk.

## The hooks

| Function | Fires when |
|---|---|
| `on_single_click(ctx)` | the item is single-clicked |
| `on_double_click(ctx)` | the item is double-clicked |
| `on_drop(ctx)` | the item is put down, on the ground or into a container |
| `on_equip(ctx)` | the item is placed on a paperdoll layer |
| `on_unequip(ctx)` | the item is taken off a layer |

One name per hook. There are no alternative spellings and no fallback on the item's name.

`on_equip` and `on_unequip` fire for **every** equip, not just player-driven ones — the starting kit
a new character is dressed in raises them too.

## The context table

Every hook receives one argument.

| Field | Always present | Meaning |
|---|---|---|
| `ctx.hook` | yes | The hook's own name, e.g. `"on_double_click"`. |
| `ctx.item` | yes | The item — see below. |
| `ctx.container_id` | yes | Serial of the container it was dropped into; `0` for a ground drop, and `0` in every hook other than `on_drop`. |
| `ctx.actor` | **no** | Who caused it. |
| `ctx.layer` | **no** | Layer name as a string. Only in `on_equip` / `on_unequip`. |

`ctx.item` carries `serial`, `item_id`, `template_id`, `script_id`, `name`, `amount`, `hue`,
`map_id`, `x`, `y`, `z`.

`ctx.actor`, when present, carries `serial`, `name`, `map_id`, `x`, `y`, `z`.

> [!IMPORTANT]
> **Always check `ctx.actor` before using it.** It is absent whenever nothing player-driven caused
> the hook — a character being dressed in its starting kit raises `on_equip` with no actor — and
> also when the clicking session has gone away between the click and the dispatch.

```lua
function magic_torch.on_equip(ctx)
    if ctx.actor == nil then
        return
    end

    log.info(ctx.actor.name .. " equipped a torch on " .. ctx.layer)
end
```

## What a hook may do

Hooks run **on the game-loop thread**, so they may touch world state directly — call `item.*`,
`mobile.*` and the rest of the [scripting API](../index.md) without wrapping anything in
`game.post`.

## Limits

The runtime is deliberately unforgiving, so a content mistake cannot take the server with it:

- **A script id is dot-separated lowercase segments** — `^[a-z0-9_]+(\.[a-z0-9_]+)*$`. No path
  separator and no empty segment can match, so no id can name a parent directory and nothing resolves
  outside `scripts/`. A bare id with no dot, like `magic_torch`, is a script at the root of
  `scripts/`.
- **A file may not exceed 256 KiB**, and may not be a symbolic link.
- **A hook gets 50 000 VM instructions.** Exceeding the budget aborts that call with a warning.
- **A hook may not yield.**
- **Errors are logged, never thrown.** A hook that raises leaves the item inert rather than faulting
  the game loop.

A script is loaded the first time its id is needed and then kept, misses included — a typo in a
template does not re-hit the disk on every click. There is **no hot reload**: unlike brains, changing
an item script needs a restart.

## Shipped example

`magic_torch.lua` ships with the server and is copied into `<root>/scripts/items/` on first use, if
it is not already there. An existing file is never overwritten, so your edits survive upgrades.
