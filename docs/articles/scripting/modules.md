# Script Modules

Creating custom script modules in Moongate v2.

## Overview

Script modules allow you to expose .NET functionality to Lua scripts. This enables powerful customization without recompiling the server.

## Built-In Runtime Modules (Current)

The following modules are available in the default server runtime:

- `log`
- `command`
- `speech`
- `dialogue`
- `ai_dialogue`
- `combat`
- `mobile`
- `item`
- `door`
- `effect`
- `gump`
- `text`
- `location`
- `random`
- `dice`
- `timer`
- `time`
- `weather`
- `map`
- `convert`

Effect module runtime helpers:

```lua
effect.send(mapId, x, y, z, itemId, speed, duration, hue, renderMode, effect, explodeEffect, explodeSound, layer, unknown3)
effect.send_to_player(characterId, x, y, z, itemId, speed, duration, hue, renderMode, effect, explodeEffect, explodeSound, layer, unknown3)
```

`mobile` runtime helper also supports:

```lua
local npc = mobile.get(serial)
if npc then
  npc:SetEffect(0x3728, 10, 10, 0, 0, 2023)
  npc:say("Hello there.")
  npc:emote("*looks furious*")
  npc:yell("Leave now!")
  npc:whisper("Not so loud...")
end
```

Speech-related runtime notes:

- `npc:say(text)` emits regular world speech
- `npc:emote(text)` emits world emote speech with `ChatMessageType.Emote`
- `npc:yell(text)` emits world yell speech with `ChatMessageType.Yell`
- `npc:whisper(text)` emits world whisper speech with `ChatMessageType.Whisper`
- player incoming speech uses the same world speech path
- incoming player text shorthand is normalized automatically:
  - `*text*` -> emote
  - `!text` -> yell
  - `;text` -> whisper

`dialogue` runtime helpers:

```lua
dialogue.register("innkeeper", definition)
dialogue.init(npc, "innkeeper")
dialogue.listener(npc, speaker, text)
```

The repository also ships DSL helpers in `common.dialogue`:

```lua
local dialogue = require("common.dialogue")
```

`dialogue` is a standalone authored feature. It does not require `ai_dialogue` or any OpenAI configuration.

`ai_dialogue` runtime helpers:

```lua
ai_dialogue.init(npc, "innkeeper.txt")
ai_dialogue.listener(npc, speaker, text)
ai_dialogue.idle(npc)
```

`ai_dialogue` is separate and optional. It is only needed when you want OpenAI-backed generative replies.

Recommended bridge helper:

```lua
local npc_dialogue = require("common.npc_dialogue")
```

Use `common.npc_dialogue` only when you explicitly want both systems together.

`combat` runtime helpers:

```lua
combat.set_target(npcSerial, targetSerial) -- hands control to the server combat loop
combat.clear_target(npcSerial)             -- clears combatant and pending swing
combat.swing(npcSerial, targetSerial)      -- animation helper only, not authoritative combat
```

`gump` supports two modes:

- runtime builder (`gump.create()` + `gump.send(...)` + `gump.on(...)`)
- file-based layout (`gump.send_layout(...)`) using files under `moongate_data/scripts/gumps/**`

`text` renders Scriban text templates from:

- `moongate_data/scripts/texts/**`

Example:

```lua
local body = text.render("welcome_player.txt", {
  player = {
    name = "Tommy"
  }
})
```

Built-in template values available by default:

- `shard.name`
- `shard.website_url`

Text template comments:

- `# comment` skips the whole line
- `value # comment` keeps only `value`
- `\#` preserves a literal `#`

`location` runtime helpers:

```lua
local total = location.count()
local first = location.get(1)       -- 1-based index, nil if out of range
local by_name = location.find("Britain")
```

`map` runtime helpers:

```lua
map.to_id("felucca")    -- returns 0
map.to_id("trammel")    -- returns 1
map.to_id("ilshenar")   -- returns 2
map.to_id("malas")      -- returns 3
map.to_id("tokuno")     -- returns 4
map.to_id("termur")     -- returns 5
map.to_id("internal")   -- returns 0x7F
map.to_id(0)            -- also accepts numeric input
```

`convert` runtime helpers:

```lua
convert.to_bool("true")         -- returns true
convert.to_bool(1)              -- returns true
convert.to_int("42")            -- returns 42
convert.parse_delay_ms("500ms") -- returns 500
convert.parse_point3d("100,200,0") -- returns {x=100, y=200, z=0}
```

## Creating a Module

### Basic Module

```csharp
using Moongate.Scripting.Attributes;

[ScriptModule("custom")]
public sealed class CustomModule
{
    [ScriptFunction("greet")]
    public string Greet(string name)
    {
        return $"Hello, {name}! Welcome to Moongate v2.";
    }
    
    [ScriptFunction("add")]
    public int Add(int a, int b)
    {
        return a + b;
    }
}
```

### Usage in Lua

```lua
-- Use custom module
local message = custom.greet("Player")
log.info(message)  -- "Hello, Player! Welcome to Moongate v2."

local sum = custom.add(5, 3)
log.info("Sum: " .. sum)  -- "Sum: 8"
```

## Module Registration

### Automatic Registration

Modules are automatically registered through source generation.

- Any class decorated with `[ScriptModule(...)]` in `Moongate.Scripting` or `Moongate.Server` is picked up at compile time.
- `Moongate.Generators` emits:
  - `Moongate.Scripting.Generated.ScriptModuleRegistry`
  - `Moongate.Server.Generated.ScriptModuleRegistry`
- Bootstrap calls both registries during startup.
- Each discovered module is registered via `container.RegisterScriptModule<TModule>()`.

This removes runtime reflection scanning for module discovery.

### Manual Registration

```csharp
// In Program.cs or startup code
services.AddSingleton<CustomModule>();

// Then in bootstrap
var customModule = serviceProvider.GetRequiredService<CustomModule>();
luaEngine.RegisterModule(customModule);
```

## ScriptFunction Attribute

### Basic Function

```csharp
[ScriptFunction("function_name")]
public void MyFunction()
{
    // Function body
}
```

### Function with Parameters

```csharp
[ScriptFunction("spawn_mobile")]
public Serial SpawnMobile(int bodyId, int hue, int x, int y, int z)
{
    var mobile = _mobileService.Create(bodyId, hue, new Point3D(x, y, z));
    return mobile.Serial;
}
```

### Function with Return Value

```csharp
[ScriptFunction("get_player_count")]
public int GetPlayerCount()
{
    return _sessionManager.ActiveCount;
}
```

### Async Function

```csharp
[ScriptFunction("save_world")]
public async Task SaveWorldAsync()
{
    await _persistenceService.SaveSnapshotAsync(CancellationToken.None);
}
```

### Function with Optional Parameters

```csharp
[ScriptFunction("broadcast")]
public void Broadcast(string message, bool adminOnly = false)
{
    foreach (var session in _sessionManager.GetAllSessions())
    {
        if (!adminOnly || session.IsAdmin)
        {
            session.SendPacket(new BroadcastPacket(message));
        }
    }
}
```

## ScriptConstant Attribute

### Exposing Constants

```csharp
[ScriptModule("server")]
public sealed class ServerModule
{
    [ScriptConstant("VERSION")]
    public string Version => "0.7.0";
    
    [ScriptConstant("MAX_PLAYERS")]
    public int MaxPlayers => 1000;
    
    [ScriptConstant("UO_PROTOCOL_VERSION")]
    public int ProtocolVersion => 7;
}
```

### Usage in Lua

```lua
-- Access constants
log.info("Server version: " .. server.VERSION)
log.info("Max players: " .. server.MAX_PLAYERS)

if player_count >= server.MAX_PLAYERS then
    log.warning("Server is full!")
end
```

## Dependency Injection

### Constructor Injection

```csharp
[ScriptModule("game")]
public sealed class GameModule
{
    private readonly ILogger<GameModule> _logger;
    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _sessionService;
    
    public GameModule(
        ILogger<GameModule> logger,
        IMobileService mobileService,
        IGameNetworkSessionService sessionService)
    {
        _logger = logger;
        _mobileService = mobileService;
        _sessionService = sessionService;
    }
    
    [ScriptFunction("spawn_npc")]
    public Serial SpawnNpc(string npcId, int x, int y, int z)
    {
        _logger.LogInformation("Spawning NPC {NpcId} at {Location}", 
            npcId, new Point3D(x, y, z));
        
        var template = _npcTemplateService.GetTemplate(npcId);
        var mobile = _mobileService.Create(template.Body, template.Hue, new Point3D(x, y, z));
        
        return mobile.Serial;
    }
}
```

## Error Handling

### Try-Catch in Modules

```csharp
[ScriptFunction("safe_operation")]
public ScriptResult SafeOperation(int value)
{
    try
    {
        var result = PerformRiskyOperation(value);
        return ScriptResult.Success(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Operation failed");
        return ScriptResult.Error("Operation failed: " + ex.Message);
    }
}
```

### Validation

```csharp
[ScriptFunction("teleport_player")]
public ScriptResult TeleportPlayer(Serial playerSerial, int x, int y, int z)
{
    // Validate coordinates
    if (x < 0 || x > 5119 || y < 0 || y > 4095)
    {
        return ScriptResult.Error("Invalid coordinates");
    }
    
    // Validate player exists
    var player = _mobileService.GetBySerial(playerSerial);
    if (player == null)
    {
        return ScriptResult.Error("Player not found");
    }
    
    // Perform teleport
    _mobileService.Teleport(player, new Point3D(x, y, z));
    return ScriptResult.Success();
}
```

## Advanced Patterns

### Event Subscription

```csharp
[ScriptModule("events")]
public sealed class EventModule : IDisposable
{
    private readonly IGameEventBusService _eventBus;
    private readonly LuaScriptEngineService _luaEngine;
    
    public EventModule(IGameEventBusService eventBus, LuaScriptEngineService luaEngine)
    {
        _eventBus = eventBus;
        _luaEngine = luaEngine;
        
        // Subscribe to events
        _eventBus.Subscribe<PlayerConnectedEvent>(OnPlayerConnected);
    }
    
    private void OnPlayerConnected(PlayerConnectedEvent evt)
    {
        // Call Lua callback
        _luaEngine.CallFunction("on_player_connected", evt.PlayerSerial);
    }
    
    public void Dispose()
    {
        _eventBus.Unsubscribe<PlayerConnectedEvent>(OnPlayerConnected);
    }
}
```

### State Management

```csharp
[ScriptModule("storage")]
public sealed class StorageModule
{
    private readonly ConcurrentDictionary<string, object> _storage = new();
    
    [ScriptFunction("set")]
    public void Set(string key, object value)
    {
        _storage[key] = value;
    }
    
    [ScriptFunction("get")]
    public object? Get(string key)
    {
        return _storage.GetValueOrDefault(key);
    }
    
    [ScriptFunction("delete")]
    public bool Delete(string key)
    {
        return _storage.TryRemove(key, out _);
    }
    
    [ScriptFunction("clear")]
    public void Clear()
    {
        _storage.Clear();
    }
}
```

## Best Practices

### DO:

- Use dependency injection for services
- Validate all input parameters
- Handle exceptions gracefully
- Use async/await for I/O operations
- Document your modules with XML comments
- Keep modules focused and single-purpose

### DON'T:

- Expose sensitive operations without authorization
- Block in script functions (use async)
- Store mutable state without thread safety
- Create circular dependencies between modules
- Expose internal implementation details

## Complete Example

### Chat Command Module

```csharp
using System.Collections.Concurrent;
using Moongate.Scripting.Attributes;

/// <summary>
/// Provides chat command functionality to Lua scripts.
/// </summary>
[ScriptModule("commands")]
public sealed class ChatCommandModule
{
    private readonly ILogger<ChatCommandModule> _logger;
    private readonly IGameNetworkSessionService _sessionService;
    private readonly ConcurrentDictionary<string, Func<Serial, string, Task>> _commands = new();
    
    public ChatCommandModule(
        ILogger<ChatCommandModule> logger,
        IGameNetworkSessionService sessionService)
    {
        _logger = logger;
        _sessionService = sessionService;
        
        // Register built-in commands
        RegisterCommand("help", OnHelpCommand);
        RegisterCommand("online", OnOnlineCommand);
    }
    
    /// <summary>
    /// Registers a new chat command.
    /// </summary>
    [ScriptFunction("register")]
    public void RegisterCommand(string name, Func<Serial, string, Task> handler)
    {
        _commands[name.ToLower()] = handler;
        _logger.LogInformation("Registered chat command: {Command}", name);
    }
    
    /// <summary>
    /// Processes a chat command from a player.
    /// </summary>
    [ScriptFunction("process")]
    public async Task<ScriptResult> ProcessCommandAsync(Serial playerSerial, string text)
    {
        if (!text.StartsWith("/"))
        {
            return ScriptResult.Success(false);  // Not a command
        }
        
        var parts = text[1..].Split(' ', 2);
        var commandName = parts[0].ToLower();
        var arguments = parts.Length > 1 ? parts[1] : string.Empty;
        
        if (!_commands.TryGetValue(commandName, out var handler))
        {
            return ScriptResult.Error($"Unknown command: {commandName}");
        }
        
        try
        {
            await handler(playerSerial, arguments);
            return ScriptResult.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {Command} failed", commandName);
            return ScriptResult.Error("Command failed: " + ex.Message);
        }
    }
    
    private async Task OnHelpCommand(Serial playerSerial, string arguments)
    {
        var session = _sessionManager.GetGameSession(playerSerial);
        session?.SendPacket(new MessagePacket("Available commands: /help, /online"));
    }
    
    private async Task OnOnlineCommand(Serial playerSerial, string arguments)
    {
        var session = _sessionManager.GetGameSession(playerSerial);
        var count = _sessionManager.ActiveCount;
        session?.SendPacket(new MessagePacket($"Players online: {count}"));
    }
}
```

### Usage in Lua

```lua
-- Register custom command
commands.register("announce", function(playerSerial, args)
    local player = game.get_player(playerSerial)
    if not player.IsAdmin then
        return false, "You must be an admin!"
    end
    
    game.broadcast("[ANNOUNCEMENT] " .. player.Name .. ": " .. args)
    return true
end)

-- Process command (called from on_player_speech)
function on_player_speech(player, text)
    local success, result = commands.process(player.Serial, text)
    
    if success and result then
        return true  -- Command was processed
    end
    
    return false  -- Normal speech
end
```

## Next Steps

- **[API Reference](api.md)** - Complete scripting API
- **[Overview](overview.md)** - Scripting introduction
- **[Event System](../architecture/events.md)** - Event-driven architecture

---

**Previous**: [Overview](overview.md) | **Next**: [API Reference](api.md)
