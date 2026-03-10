# Lua Scripting

Moongate v2 includes a powerful Lua scripting subsystem for gameplay customization.

## Overview

The scripting system is built on **MoonSharp**, a lightweight Lua interpreter for .NET. It provides:

- Full Lua 5.2 compatibility
- .NET interop via attributes
- Automatic `.luarc` generation for editor tooling
- Callback system for game events

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Scripting System                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐   │
│  │ Lua Scripts  │───▶│  Script      │───▶│  .NET        │   │
│  │ (.lua files) │    │  Engine      │    │  Modules     │   │
│  └──────────────┘    └──────────────┘    └──────────────┘   │
│                             │                    │           │
│                             │                    ▼           │
│                        ┌────┴────┐    ┌──────────────┐      │
│                        │ .luarc  │    │  Game        │      │
│                        │ Generator│   │  Events      │      │
│                        └─────────┘    └──────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

### Create Your First Script

Create `scripts/init.lua`. This file is the main entry point and uses `require` to load all
script modules (AI brains, commands, item scripts):

```lua
-- init.lua - main entry point
-- Require AI brains
require("ai/orion")

-- Require GM commands
require("commands/gm/eclipse")
require("commands/gm/set_world_light")
require("commands/gm/teleports")
require("commands/gm/spawn_tools")

-- Require item scripts
require("items/apple")
require("items/brick")
require("items/door")
require("items/teleport")

-- Called when a player connects to the server
function on_player_connected(p)
    log.info("Player connected: " .. tostring(p.name))
end

-- Called when the scripting engine is ready
function on_ready()
    log.info("Scripts loaded and ready")
end
```

### Create a Script Module

Create a .NET module to expose to Lua:

```csharp
using Moongate.Scripting.Attributes;

[ScriptModule("server")]
public sealed class ServerModule
{
    private readonly ILogger _logger;
    
    public ServerModule(ILogger logger)
    {
        _logger = logger;
    }
    
    [ScriptFunction("broadcast")]
    public void Broadcast(string message)
    {
        _logger.LogInformation("Broadcast: {Message}", message);
        // Send to all players...
    }
    
    [ScriptFunction("get_player_count")]
    public int GetPlayerCount()
    {
        return _sessionManager.ActiveCount;
    }
}
```

### Use Module in Lua

```lua
-- Broadcast to all players
server.broadcast("Welcome to Moongate v2!")

-- Get player count
local count = server.get_player_count()
log.info("Active players: " .. count)
```

## Script Examples (NPC / Item / Gump / Command)

For the behavior-based NPC AI architecture, see [NPC Behaviors](npc-behaviors.md).
For vendor sell profiles and context menu flow (native + custom Lua), see
[Vendor and Context Menus](vendor-context-menus.md).

### NPC Brain Example

`mobileTemplate.brain = "orion"` resolves to table `orion` in `scripts/ai/orion.lua`.

```lua
orion = {}

function orion.brain_loop(npc_id)
    while true do
        local npc = mobile.get(npc_id)
        if npc ~= nil then
            npc:move(random.direction())
        end

        coroutine.yield(250)
    end
end

function orion.on_speech(ctx)
    -- ctx.source_serial, ctx.text, ctx.range...
end
```

### Item Script Example

`item.script_id = "apple"` resolves to table `apple` in `scripts/items/apple.lua`.

```lua
apple = {
    on_double_click = function(ctx)
        if ctx == nil or ctx.item == nil then
            return
        end

        local serial = convert.to_int(ctx.item.serial, 0)
        if serial <= 0 then
            return
        end

        local proxy = item.get(serial)
        if proxy ~= nil and proxy:delete() and ctx.session_id ~= nil then
            speech.send(ctx.session_id, "You eat the apple.")
        end
    end
}
```

### Gump Script Example

`gump.send_layout` with inline callback handlers.

```lua
local sample = {}

function sample.open(session_id, character_id)
    local layout = {
        ui = {
            { type = "background", x = 0, y = 0, gump_id = 9200, width = 320, height = 160 },
            { type = "label", x = 24, y = 20, hue = 1152, text = "Spawn Tools" },
            { type = "button", id = 101, x = 24, y = 52, normal_id = 4005, pressed_id = 4007, onclick = "on_spawn" }
        },
        handlers = {}
    }

    layout.handlers.on_spawn = function(ctx)
        command.execute("spawn_doors", 1)
        speech.send(ctx.session_id, "Spawn complete.")
    end

    return gump.send_layout(session_id, layout, character_id or 0, 0xB220, 120, 80)
end

return sample
```

### Lua Command Example

Register a GM-only command in `scripts/commands/gm/eclipse.lua`.

```lua
command.register("eclipse", function(ctx)
    weather.set_global_light(26)
    speech.broadcast("The moon has blocked the sun.")

    if ctx.session_id ~= nil and ctx.session_id > 0 then
        ctx:print("Eclipse started.")
    end
end, {
    description = "Starts a world eclipse and broadcasts a global message.",
    minimum_account_type = "Administrator"
})
```

## Script Modules

### Defining Modules

Modules are .NET classes exposed to Lua:

```csharp
using Moongate.Scripting.Attributes;

[ScriptModule("game")]
public sealed class GameModule
{
    [ScriptFunction("spawn_mobile")]
    public Serial SpawnMobile(int bodyId, int hue, Point3D location)
    {
        // Spawn mobile logic
        return mobile.Serial;
    }
    
    [ScriptFunction("spawn_item")]
    public Serial SpawnItem(int itemId, int amount, Point3D location)
    {
        // Spawn item logic
        return item.Serial;
    }
    
    [ScriptFunction("get_time")]
    public DateTime GetTime()
    {
        return DateTime.UtcNow;
    }
}
```

### ScriptFunction Attributes

```csharp
[ScriptFunction("name")]  // Expose function with custom name
[ScriptFunction("name", "Help text shown in generated docs")]  // Optional help text
```

### ScriptConstant Attributes

```csharp
[ScriptConstant("VERSION")]
public string Version => "0.7.0";

[ScriptConstant("MAX_PLAYERS")]
public int MaxPlayers => 1000;
```

## Callbacks

### Available Callbacks

```lua
-- Player events
function on_player_connected(player) end
function on_player_disconnected(player) end
function on_player_speech(player, text) end
function on_player_use_item(player, item) end

-- World events
function on_server_start() end
function on_server_stop() end
function on_tick() end  -- Called every game tick
```

## NPC Brain Loop

NPC templates can bind a Lua brain through `brain`:

```json
{
  "type": "mobile",
  "id": "orione",
  "body": "0x00C9",
  "skinHue": 779,
  "brain": "orion",
  "name": "Orione",
  "title": "a beautiful cat"
}
```

The value `brain: "orion"` resolves to `scripts/ai/orion.lua`.

Real brain script (Orion the cat NPC):

```lua
-- ai/orion.lua - coroutine-based NPC brain for a cat

local MOVE_INTERVAL  = 1000  -- ms between random movements
local SPEECH_INTERVAL = 2000 -- ms between random speech
local SOUND_INTERVAL  = 3000 -- ms between random sounds

local speeches = {
    "Meow!",
    "Purrr...",
    "*stretches lazily*",
    "*rubs against your leg*"
}

local last_move  = 0
local last_speech = 0
local last_sound  = 0

function brain_loop(npc_id)
    while true do
        local now = os.clock() * 1000

        -- Random movement
        if now - last_move >= MOVE_INTERVAL then
            npc.random_move(npc_id)
            last_move = now
        end

        -- Random speech from table
        if now - last_speech >= SPEECH_INTERVAL then
            local phrase = speeches[math.random(#speeches)]
            npc.say(npc_id, phrase)
            last_speech = now
        end

        -- Random sound effect
        if now - last_sound >= SOUND_INTERVAL then
            npc.play_sound(npc_id, 0xDB) -- cat sound
            last_sound = now
        end

        coroutine.yield(250)
    end
end

function on_event(event_type, from_serial, event_obj)
    if event_type ~= "speech_heard" or event_obj == nil then
        return
    end

    local text = event_obj.text
    if string.find(string.lower(text), "hello", 1, true) then
        npc.say(event_obj.listener_npc_id, "Meow!")
    end
end

function on_death(npc_id)
    log.info("Orion has died: " .. tostring(npc_id))
end
```

Notes:

- `brain_loop` is resumed by the server tactical runner.
- `coroutine.yield(ms)` controls the next brain tick delay.
- `on_event(eventType, fromSerial, eventObject)` is the primary callback for runtime brain events.
- Current event type: `speech_heard`.
- `eventObject` fields for speech: `listener_npc_id`, `speaker_id`, `text`, `speech_type`, `map_id`, `location` (`x`, `y`, `z`).
- Legacy `on_speech(listener_npc_id, speaker_id, text, speech_type, map_id, x, y, z)` remains supported for compatibility.

## Item `ScriptId` Hooks

Moongate supports item-scoped script dispatch through `IItemScriptDispatcher`.

- Source field: `UOItemEntity.ScriptId` (usually filled by item templates via `scriptId`)
- Runtime input: `ItemScriptContext` (`Session`, `Mobile`, `Item`, `Hook`, `Metadata`)
- Dispatch model:
  - if `scriptId` is set and not `none`, normalized `scriptId` resolves a Lua table
  - if `scriptId == "none"`, fallback table candidates are:
    - `<normalized_item_name>`
    - `items_<normalized_item_name>`
  - hook resolves a function on that table

Dispatch example:

- `scriptId`: `apple` resolves table `apple`
- `double_click` tries: `on_double_click`, `OnDoubleClick`

### Apple (simple: delete item and send message)

```lua
-- items/apple.lua
apple = {
    on_double_click = function(ctx)
        item.delete(ctx.item.serial)
        speech.send_message(ctx.session_id, "You eat the apple.")
    end
}
```

### Door (toggle state with open/close sounds)

```lua
-- items/door.lua
door = {
    on_double_click = function(ctx)
        local is_open = door.toggle(ctx.item.serial)
        if is_open then
            sound.play(ctx.session_id, 0xEA) -- open sound
        else
            sound.play(ctx.session_id, 0xEC) -- close sound
        end
    end
}
```

### Teleport (metadata parsing, effects, optional delay)

```lua
-- items/teleport.lua
teleport = {
    on_double_click = function(ctx)
        local meta = ctx.metadata
        local dest_map = meta.dest_map
        local dest = convert.parse_point3d(meta.dest_x, meta.dest_y, meta.dest_z)

        -- Play departure effect and sound
        effect.play_at(ctx.item.serial, 0x3728)
        sound.play(ctx.session_id, 0x1FE)

        -- Optional delay before teleport
        timer.after(500, function()
            mobile.teleport(ctx.mobile.serial, dest.x, dest.y, dest.z, dest_map)
            sound.play(ctx.session_id, 0x1FE) -- arrival sound
        end)
    end
}
```

## Gump Callbacks

Lua gumps support response callbacks via packet `0xB1`:

- `gump.send(sessionId, builder, senderSerial, gumpId, x, y)`
- `gump.on(gumpId, buttonId, callback)`
- `gump.send_layout(sessionId, layoutTable, senderSerial, gumpId, x, y, ctx?)` (recommended)

Example:

```lua
local FIRST_GUMP = 0xB10C
local SECOND_GUMP = 0xB10D
local OPEN_NEXT = 1

gump.on(FIRST_GUMP, OPEN_NEXT, function(ctx)
    local g2 = gump.create()
    g2:resize_pic(0, 0, 9200, 260, 120)
    g2:text(20, 20, 1152, "Second gump")
    gump.send(ctx.session_id, g2, ctx.character_id or 0, SECOND_GUMP, 140, 90)
end)
```

File-based layout example:

```lua
-- moongate_data/scripts/gumps/test_shop.lua
return {
    ui = {
        { type = "page", index = 0 },
        { type = "background", x = 0, y = 0, gump_id = 9200, width = 320, height = 180 },
        { type = "label", x = 20, y = 20, hue = 1152, text = "Hello $ctx.name" },
        { type = "button", id = 1, x = 20, y = 130, normal_id = 4005, pressed_id = 4007, onclick = "open_next" }
    },
    handlers = {
        open_next = function(cb_ctx)
            log.info("Button: " .. tostring(cb_ctx.button_id))
        end
    }
}
```

```lua
local layout = require("gumps/test_shop")
local ui_ctx = { name = "Orion", level = 42 }
gump.send_layout(session_id, layout, character_id, 0xB300, 120, 80, ui_ctx)
```

`ctx` placeholders are supported in text fields for file-based layouts:

- `$ctx.name`
- `$ctx.level`

### Callback Parameters

```lua
function on_player_speech(player, text)
    -- player: { Serial, Name, Position, Account }
    -- text: string
    
    -- Log speech
    log.info(player.Name .. " says: " .. text)
    
    -- Process commands
    if text:starts_with("/") then
        process_command(player, text)
    end
end
```

## API Reference

### Log Module

```lua
log.debug(message)      -- Debug level
log.info(message)       -- Info level
log.warning(message)    -- Warning level
log.error(message)      -- Error level
log.critical(message)   -- Critical level
```

### Server Module

```lua
server.broadcast(message)           -- Broadcast to all players
server.get_player_count()           -- Get active player count
server.get_player(serial)           -- Get player by serial
server.shutdown()                   -- Graceful shutdown
server.save_world()                 -- Save world state
```

### Game Module

```lua
game.spawn_mobile(body, hue, x, y, z, map)  -- Spawn mobile
game.spawn_item(itemId, amount, x, y, z)    -- Spawn item
game.get_mobile(serial)                     -- Get mobile data
game.get_item(serial)                       -- Get item data
game.move_object(serial, x, y, z)           -- Move object
game.delete_object(serial)                  -- Delete object
```

### Player Module

```lua
player.send_message(text)           -- Send message to player
player.send_gump(gumpId, data)      -- Send gump dialog
player.teleport(x, y, z, map)       -- Teleport player
player.add_item(itemId, amount)     -- Add item to backpack
player.remove_item(serial, amount)  -- Remove item
player.get_skill(skillName)         -- Get skill value
player.set_skill(skillName, value)  -- Set skill value
```

### World Module

```lua
world.get_time()                    -- Get server time
world.get_tile(x, y, z, map)        -- Get tile info
world.get_region(x, y, map)         -- Get region name
world.spawn_npc(mobileId, x, y, z)  -- Spawn NPC
world.despawn(serial)               -- Despawn object
```

## Configuration

### Script Settings

```json
{
  "scripting": {
    "enabled": true,
    "scriptsDirectory": "scripts",
    "autoReload": false,
    "debugMode": false,
    "timeoutMilliseconds": 5000
  }
}
```

### Script Directories

Scripts are loaded from:

```
moongate_data/scripts/
├── init.lua
├── ai/
│   └── orion.lua
├── commands/
│   └── gm/
│       ├── eclipse.lua
│       ├── set_world_light.lua
│       ├── spawn_tools.lua
│       └── teleports.lua
├── gumps/
│   ├── spawn_tools.lua
│   └── teleports/
│       ├── constants.lua
│       ├── controller.lua
│       ├── data.lua
│       ├── state.lua
│       ├── ui.lua
│       ├── render.lua
│       └── actions.lua
└── items/
    ├── apple.lua
    ├── brick.lua
    ├── door.lua
    └── teleport.lua
```

## Editor Tooling

### .luarc.json Generation

Moongate v2 automatically generates `.luarc.json` for editor support:

```json
{
  "workspace.library": [
    "/path/to/moongatev2/scripts/definitions"
  ],
  "diagnostics.disable": [],
  "runtime.version": "Lua 5.2"
}
```

### TypeScript-like Definitions

Auto-generated `definitions.lua`:

```lua
---@class Player
---@field Serial number
---@field Name string
---@field Position Position

---@class LogModule
log = {}

---@param message string
function log.debug(message) end

---@param message string
function log.info(message) end

---@class ServerModule
server = {}

---@param message string
function server.broadcast(message) end

---@return number
function server.get_player_count() end
```

### VS Code Setup

1. Install **Lua Language Server** extension
2. Open scripts folder in VS Code
3. Definitions are auto-generated on server start
4. Enjoy IntelliSense and type checking!

## Error Handling

### Script Errors

```csharp
try
{
    _luaEngine.CallFunction("on_player_connected", player);
}
catch (ScriptRuntimeException ex)
{
    logger.LogError(ex, "Script error in on_player_connected");
}
```

### Timeout Protection

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try
{
    await _luaEngine.ExecuteAsync(script, cts.Token);
}
catch (OperationCanceledException)
{
    logger.LogWarning("Script execution timed out");
}
```

## Performance

### Best Practices

**DO:**
- Cache function references
- Use local variables
- Minimize .NET interop calls
- Batch operations

**DON'T:**
- Create tables in loops
- Use global variables excessively
- Call .NET functions in tight loops
- Block in callbacks

### Example: Efficient Script

```lua
-- GOOD: Cached references
local log_info = log.info
local server_broadcast = server.broadcast

local function process_player(player)
    local name = player.Name  -- Cache property
    log_info("Processing: " .. name)
end

-- BAD: Repeated lookups
function process_player(player)
    log.info("Processing: " .. player.Name)
    server.broadcast("Processing: " .. player.Name)
end
```

## Testing

### Unit Testing Scripts

```csharp
[Fact]
public void Script_OnPlayerConnected_CallsLogInfo()
{
    var engine = CreateScriptEngine();
    var mockLogger = new Mock<ILogger>();
    
    engine.RegisterModule("log", mockLogger.Object);
    engine.LoadScript("init.lua");
    
    engine.CallFunction("on_player_connected", testPlayer);
    
    mockLogger.Verify(l => l.Info(It.IsAny<string>()), Times.Once);
}
```

## Examples

### GM Command - Eclipse

```lua
-- commands/gm/eclipse.lua
-- Activates eclipse mode: sets global darkness and broadcasts to all players

commands_gm_eclipse = {
    on_command = function(ctx)
        weather.set_global_light(26)
        speech.broadcast("Eclipse mode activated!")
        log.info("Eclipse mode enabled by " .. tostring(ctx.mobile.serial))
    end
}
```

### GM Command - Set World Light

```lua
-- commands/gm/set_world_light.lua
-- Usage: .set_world_light <0-255>

commands_gm_set_world_light = {
    on_command = function(ctx)
        local value = tonumber(ctx.args)
        if value == nil or value < 0 or value > 255 then
            speech.send_message(ctx.session_id, "Usage: .set_world_light <0-255>")
            return
        end
        weather.set_global_light(value)
        log.info("World light set to " .. tostring(value))
    end
}
```

### NPC Brain - Orion the Cat

```lua
-- ai/orion.lua
-- Coroutine-based brain for a cat NPC with random movement, speech, and sounds

local MOVE_INTERVAL  = 1000
local SPEECH_INTERVAL = 2000
local SOUND_INTERVAL  = 3000

local speeches = {
    "Meow!",
    "Purrr...",
    "*stretches lazily*",
    "*rubs against your leg*"
}

function brain_loop(npc_id)
    local last_move, last_speech, last_sound = 0, 0, 0
    while true do
        local now = os.clock() * 1000
        if now - last_move >= MOVE_INTERVAL then
            npc.random_move(npc_id)
            last_move = now
        end
        if now - last_speech >= SPEECH_INTERVAL then
            npc.say(npc_id, speeches[math.random(#speeches)])
            last_speech = now
        end
        if now - last_sound >= SOUND_INTERVAL then
            npc.play_sound(npc_id, 0xDB)
            last_sound = now
        end
        coroutine.yield(250)
    end
end

function on_event(event_type, from_serial, event_obj)
    if event_type ~= "speech_heard" or event_obj == nil then return end
    if string.find(string.lower(event_obj.text), "hello", 1, true) then
        npc.say(event_obj.listener_npc_id, "Meow!")
    end
end

function on_death(npc_id)
    log.info("Orion has died: " .. tostring(npc_id))
end
```

## Next Steps

- **[Script Modules](modules.md)** - Create custom modules
- **[API Reference](api.md)** - Full API documentation
- **[Persistence](../persistence/overview.md)** - Data storage

---

**Previous**: [Solution Structure](../architecture/solution.md) | **Next**: [Script Modules](modules.md)
