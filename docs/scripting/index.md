# Scripting

Moongate shards are scripted in **Lua**. The server exposes a small, curated
surface of global modules that let scripts log, run work on the game loop,
subscribe to server events, and create or manipulate items and mobiles by
serial. Each module is a C# class bridged into the Lua runtime (the SquidStd
scripting engine, built on MoonSharp).

## Modules

| Module | Purpose | Reference |
|---|---|---|
| `log` | Write to the server log (Serilog). | [log](reference/log.md) |
| `game` | Run work on the game-loop thread and schedule timers. | [game](reference/game.md) |
| `events` | Subscribe to named server events. | [events](reference/events.md) |
| `item` | Create and manipulate items by serial. | [item](reference/item.md) |
| `mobile` | Create and manipulate mobiles by serial. | [mobile](reference/mobile.md) |
| `loot` | Roll loot tables into items. | [loot](reference/loot.md) |
| `account` | Create and manage accounts by username. | [account](reference/account.md) |
| enums | `skill_name`, `gender_type`, `race_type`, `layer_type`, `account_level_type`. | [Enums](reference/enums.md) |

## Values and types

Scripts never hold C# object handles. Items and mobiles are referenced by
**serial** — a plain number that round-trips as a Lua number and is re-resolved
on every call. Accounts are the exception: they are referenced by **username**,
the handle they log in with. Functions that create something return the new serial, or `nil`
when creation failed (for example an unknown template). Functions that read
state return a Lua **table** of fields, or `nil` when the subject does not
exist. Functions that return several serials return them as a Lua
**array-table** (iterate with `ipairs`).

## Thread model

> [!WARNING]
> The `item.*`, `mobile.*` and `loot.*` functions are synchronous and mutate
> world state directly, so they must run on the **game-loop thread** — the
> single-writer boundary for world state.
>
> - Callbacks registered with `events.on` (including `world_ready`) are
>   dispatched on the game-loop thread automatically. You do **not** need to
>   wrap their body in `game.post`.
> - From any other context (an async continuation, a thread of your own —
>   anything not already running on the loop),
>   reach the loop with [`game.post`](reference/game.md#gamepost),
>   [`game.schedule`](reference/game.md#gameschedule) or
>   [`game.schedule_repeating`](reference/game.md#gameschedule_repeating).
>
> As a diagnostic safety net, every mutating `item.*` / `mobile.*` / `loot.*`
> call checks whether it is running on the loop thread and logs a **warning**
> if it is not. It never blocks or throws — it just makes an otherwise-silent
> single-writer violation visible in the log.
>
> The `account.*` functions are the exception: they touch the account store,
> not the world, and carry no such requirement. The one that does reach the
> world is [`account.delete`](reference/account.md#accountdelete) — it deletes
> the account's characters — and it warns off-loop like the rest.
