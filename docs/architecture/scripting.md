# Scripting

Moongate's scripting plugin configures the SquidStd Lua engine and exposes the infrastructure modules `log` (logging) and `game` (game-loop dispatch). A second plugin, `MoongateScriptModulesPlugin`, layers the gameplay-object modules `item` and `mobile` on top, so scripts can create and manipulate items and mobiles by serial.

## Engine and module registration

`MoongateScriptingPlugin.Configure` resolves the application metadata and directory configuration, then registers a Lua engine whose root and script directory both point to the configured `scripts` path. It supplies the host application name and version, registers Lua event integration, and registers `LoggerModule` and `GameLoopModule`.

Nothing in the plugin or modules establishes hot reload, isolation, sandboxing, or filesystem-access policy. Those properties must not be assumed from the presence of a Lua engine.

## Exposed capabilities

The `log` module forwards `info`, `warn`, `error`, and `debug` calls to Serilog, accepting a message and additional argument values.

The `game` module adapts `IGameLoopContext`:

```text
game.post(callback)
game.schedule(name, delayMs, callback) -> timer id
game.schedule_repeating(name, intervalMs, callback) -> timer id
game.cancel(timerId) -> boolean
```

Delays and intervals are converted from milliseconds to `TimeSpan`. Each closure is invoked through a wrapper that catches and logs its exception. The module test proves that posted work remains deferred until the main-thread dispatcher drains and that a scheduled timer id can be cancelled. The lower-level dispatch and timer contracts are described on the [game loop](./game-loop.md) page.

## Gameplay modules: `item` and `mobile`

`MoongateScriptModulesPlugin` registers two gameplay modules that bridge the server's item and mobile services to Lua. Both reference their subjects by **serial** (a plain number), so scripts never hold C# object handles; a serial round-trips as a number and is re-resolved on each call.

These functions are synchronous and mutate world state directly, so they must run on the game-loop thread — the single-writer boundary for item and mobile state.

**Event handlers are already loop-affine.** Callbacks registered with `events.on(...)` are dispatched on the game-loop thread automatically, so you do **not** need to wrap their body in `game.post`. Moongate installs a loop-affine invoke marshaller (`LoopAffineInvokeMarshaller`): when an event fires it runs the handler inline if the publisher is already on the loop, otherwise it posts the handler onto the loop. `game.post` / `game.schedule` / `game.schedule_repeating` are still the way to reach the loop from code that runs off it by other means (for example a bare async continuation).

**`world_ready`.** Moongate publishes a `world_ready` event on the game-loop thread once the engine has started and the world is loaded. Use it for bootstrap spawns — the handler runs on the loop, so it can mutate world state directly:

```lua
events.on("world_ready", function()
    mobile.create_from_template("guard", 1, 1420, 1690, 0)
end)
```

**Off-loop guard.** As a diagnostic safety net, every mutating `item.*` / `mobile.*` call checks whether it is running on the loop thread and logs a **warning** if it is not (`"<op> called off the game-loop thread; ..."`). It never blocks or throws — it just makes an otherwise-silent single-writer violation visible in the logs.

### `item`

```text
item.create(templateId, amount, hue)         -> serial | nil
item.create_by_tag(tag, amount, hue)         -> serial | nil
item.create_by_category(category, amount, hue) -> serial | nil
item.get(serial)  -> { id, item_id, name, amount, hue, layer, container, mobile } | nil
item.set(serial, { amount, hue, item_id, name }) -> boolean
item.flip(serial)                            -> boolean
item.delete(serial)                          -> boolean
item.equip(mobile, serial, layer)            -> boolean
item.unequip(mobile, layer)                  -> serial | nil
item.equipped(mobile)                        -> { serial, ... }
item.add_to_container(container, serial, x, y) -> boolean
item.remove_from_container(container, serial)  -> boolean
item.contents(container)                     -> { serial, ... }
```

`layer` accepts either a layer name (for example `"OneHanded"`, `"TwoHanded"`, `"Backpack"`, case-insensitive) or a `layer_type` enum constant (see [Exposed enums](#exposed-enums)).

### `mobile`

```text
mobile.create(name, map, x, y, z)            -> serial
mobile.get(serial) -> { id, name, map, x, y, z, direction, gender, race,
                        profession, str, dex, int, backpack } | nil
mobile.set(serial, fields)                   -> boolean
mobile.move(serial, x, y, z)                 -> boolean
mobile.get_skill(serial, skillName)          -> number   -- 0 when unset/unknown
mobile.set_skill(serial, skillName, value)   -> boolean
mobile.skills(serial)                        -> { [skillName] = value, ... } | nil
mobile.delete(serial)                        -> boolean
```

`mobile.set` reads the keys it recognises from `fields` and ignores the rest: `name`, `str`, `dex`, `int`, `profession`, `map`, `hair_style`, `facial_hair_style`, `skin_hue`, `hair_hue`, `facial_hair_hue`, and `gender`, `race`, `direction`. The last three accept either a name string (case-insensitive) or a numeric enum value — `gender` and `race` have exposed constants (`gender_type`, `race_type`); an unrecognised value leaves the field unchanged.

`skillName` accepts either a name string — its display form (`"Animal Lore"`) or the compact form (`"AnimalLore"`), punctuation- and case-insensitive — or a numeric skill id. The `SkillName` enum is exposed to Lua as the read-only global `skill_name`, so `skill_name.Swordsmanship` (a number) is the recommended, typo-safe way to name a skill. Skill values are stored in tenths (`500` = 50.0); the balancing rules live in `data/skills.yaml`.

### Exposed enums

`MoongateScriptModulesPlugin` exposes several C# enums to Lua as read-only, case-insensitive global tables, each mapping a member to its numeric value (for example `skill_name.Swordsmanship == 40`). Reading an undefined member yields `nil`, and the tables cannot be extended. Enums are registered with `container.RegisterScriptEnum<T>()`.

| Global | Enum | Used by |
|---|---|---|
| `skill_name` | `SkillName` | `mobile.get_skill` / `set_skill` / `skills` |
| `gender_type` | `GenderType` | `mobile.set` (`gender`) |
| `race_type` | `RaceType` | `mobile.set` (`race`) |
| `layer_type` | `LayerType` | `item.equip` / `unequip` |

Wherever a function accepts one of these, it also accepts the equivalent name string, so `item.equip(m, s, layer_type.OneHanded)` and `item.equip(m, s, "OneHanded")` are equivalent.

### Example: spawn and outfit a guard

The whole routine runs inside `game.post`, so every mutation happens on the game-loop thread. (Inside an `events.on(...)` / `world_ready` handler the `game.post` wrapper is unnecessary — the body already runs on the loop.)

```lua
game.post(function()
    -- Create a mobile on map 1 (Trammel) at (1420, 1690, 0).
    local guard = mobile.create("Town Guard", 1, 1420, 1690, 0)

    -- Appearance and stats.
    mobile.set(guard, {
        race = "Human",
        gender = "Male",
        str = 100, dex = 90, int = 25,
        skin_hue = 1002,
    })

    -- Combat skills (values are in tenths: 900 = 90.0). Reference them via the exposed
    -- skill_name enum constant, or by name string — both work.
    mobile.set_skill(guard, skill_name.Swordsmanship, 900)
    mobile.set_skill(guard, skill_name.Tactics, 850)
    mobile.set_skill(guard, "Parrying", 800)

    -- Give the guard a weapon and equip it.
    local blade = item.create("dagger", 1, 0)
    if blade and item.equip(guard, blade, layer_type.OneHanded) then
        log.info("Armed guard {0} with blade {1}", guard, blade)
    end

    -- Read state back.
    local m = mobile.get(guard)
    log.info("{0} at ({1},{2}) — Swords {3}", m.name, m.x, m.y,
        mobile.get_skill(guard, "Swordsmanship"))

    for name, value in pairs(mobile.skills(guard)) do
        log.debug("  skill {0} = {1}", name, value)
    end
end)
```

`mobile.create` returns the freshly allocated serial; the persistence store assigns it on insert. Item creation can fail (unknown template, empty category) and returns `nil`, so guard the result before using it, as the example does before equipping.

## Source map

### Runtime

- `src/Moongate.Scripting/MoongateScriptingPlugin.cs`
- `src/Moongate.Scripting/Modules/LoggerModule.cs`
- `src/Moongate.Scripting/Modules/GameLoopModule.cs`
- `src/Moongate.Scripting/LoopAffineInvokeMarshaller.cs`
- `src/Moongate.Core/Interfaces/IGameLoopContext.cs`
- `src/Moongate.Core/Interfaces/ILoopThread.cs`
- `src/Moongate.Server/Services/Game/GameLoopContext.cs`
- `src/Moongate.Server/Services/Game/LoopThreadMarker.cs`
- `src/Moongate.Server/Data/Events/WorldReadyEvent.cs`
- `src/Moongate.Server/MoongateScriptModulesPlugin.cs`
- `src/Moongate.Server/Scripting/ItemModule.cs`
- `src/Moongate.Server/Scripting/MobileModule.cs`
- `src/Moongate.Server/Scripting/LoopGuard.cs`
- `src/Moongate.Server/Scripting/ScriptEnums.cs`
- `src/Moongate.UO.Data/Types/SkillName.cs`

### Tests

- `tests/Moongate.Tests/Scripting/GameLoopModuleTests.cs`
- `tests/Moongate.Tests/Server/Game/LoopThreadMarkerTests.cs`
- `tests/Moongate.Tests/Server/Scripting/LoopAffineInvokeMarshallerTests.cs`
- `tests/Moongate.Tests/Server/Scripting/WorldReadyForwardingTests.cs`
- `tests/Moongate.Tests/Server/Scripting/LoopGuardTests.cs`
- `tests/Moongate.Tests/Server/ItemModuleTests.cs`
- `tests/Moongate.Tests/Server/MobileModuleTests.cs`
- `tests/Moongate.Tests/Server/MoongateScriptModulesPluginTests.cs`
