# Scripting API Reference

Reference for the Moongate v2 Lua scripting API.

> `definitions.lua` generated at startup is the source of truth for currently exported modules and signatures.
> This page contains legacy/planned examples too. Always validate signatures against `moongate_data/scripts/definitions.lua`.

If you are new to Moongate authoring, do not start here. Start with:

- [Create Your First Content](create-your-first-content.md)
- [Create Your First Systems](create-your-first-systems.md)
- [Create Your First Item Template](create-your-first-item-template.md)
- [Create Your First Item Script](create-your-first-item-script.md)
- [Create Your First NPC Brain](create-your-first-npc-brain.md)
- [Create Your First NPC Template](create-your-first-npc-template.md)

## Current Runtime Baseline (Verified)

The following modules are currently wired in runtime:

- `log`
- `command`
- `speech`
- `help_tickets`
- `combat`
- `steering`
- `perception`
- `npc_state`
- `mobile`
- `item`
- `bulletin`
- `dye`
- `door`
- `effect`
- `gump`
- `location`
- `random`
- `dice`
- `timer`
- `time`
- `weather` (`set_global_light`, `clear_global_light`)
- `map` (`to_id`)
- `convert` (`to_bool`, `to_int`, `parse_delay_ms`, `parse_point3d`)

23 modules total (`log` is defined in `Moongate.Scripting`, all others in `Moongate.Server`).

Common shipped command scripts:

- `moongate_data/scripts/commands/gm/eclipse.lua`
- `moongate_data/scripts/commands/gm/set_world_light.lua`
- `moongate_data/scripts/commands/gm/teleports.lua`

Common shipped helper scripts:

- `moongate_data/scripts/common/tick.lua`
- `moongate_data/scripts/common/dialogue.lua`
- `moongate_data/scripts/common/npc_dialogue.lua`
- `moongate_data/scripts/common/scheduled_events.lua`

## Real Script Examples

### Helper Script: Tick

`common.tick` is a small utility for recurring brain cadences.

```lua
local tick = require("common.tick")

local cadence = tick.state({
    move = 5000,
    speech = 2000,
})

local now = time.now_ms()

tick.run(cadence, "move", now, function()
    npc:move(random.direction())
end)

tick.run(cadence, "speech", now, function()
    npc:say("Hello there.")
end)
```

The helper exposes:

- `tick.state(intervals, start_ms?)`
- `tick.ready(state, key, now_ms)`
- `tick.run(state, key, now_ms, action?)`
- `tick.reset(state, key, now_ms, interval_ms?)`

### World Speech And Emotes

Player world speech and NPC speech use the same runtime packet family.

```lua
local npc = mobile.get(serial)
if npc then
    npc:say("Welcome, traveler.")
    npc:emote("*growls softly*")
    npc:yell("Intruder!")
    npc:whisper("Keep your voice down.")
end
```

Runtime behavior:

- player `UnicodeSpeech` is broadcast to nearby players in world range
- incoming player text supports shorthand coercion:
  - `*text*` -> `Emote`
  - `!text` -> `Yell`
  - `;text` -> `Whisper`
- `npc:emote(text)` sends `ChatMessageType.Emote` overhead speech
- `npc:yell(text)` sends `ChatMessageType.Yell` overhead speech
- `npc:whisper(text)` sends `ChatMessageType.Whisper` overhead speech
- current speech ranges are:
  - `Whisper` -> `1`
  - `Regular` / `Emote` -> `12`
  - `Yell` -> `18`
- NPC brain speech listeners still receive `speech_type`, so Lua can react differently to regular speech versus emotes

### NPC Behavior Runtime Helpers

The current NPC behavior stack uses these runtime helpers directly from Lua:

```lua
combat.set_target(npc_serial, target_serial)
combat.clear_target(npc_serial)

steering.follow(npc_serial, target_serial, 1)
steering.evade(npc_serial, target_serial, 6)
steering.move_to(npc_serial, x, y, z, 1)
steering.stop(npc_serial)

perception.distance(npc_serial, target_serial)
perception.in_range(npc_serial, target_serial, 10)
perception.find_nearest_enemy(npc_serial, 10)
perception.find_nearest_player_enemy(npc_serial, 10)

npc_state.get_hp_percent(npc_serial)
npc_state.get_var(npc_serial, "follow_target_serial")
npc_state.set_var(npc_serial, "follow_target_serial", target_serial)
```

Use canonical AI runtime keys in new Lua code:

- `ai_action`
- `ai_target_serial`

`npc_state` still accepts the legacy aliases `modernuo_action` and `modernuo_target_serial` for compatibility with older persisted NPC state. On read, the module migrates those values to the canonical keys and clears the legacy alias.

Enemy lookup is viewer-relative. The helper uses the same coarse AI relation model as Lua brain payloads:

- `Friendly`
- `Neutral`
- `Hostile`

Faction hostility and same-faction friendliness therefore affect target acquisition automatically.

These helpers are the core building blocks behind current guard behaviors such as:

- `follow`
- `ranged_keep_distance`
- `leash`
- `return_home`
- `hold_position`

### Authored Dialogue Helpers

The deterministic dialogue DSL ships as helper scripts, with runtime support provided by the `dialogue` module.

```lua
local dialogue = require("common.dialogue")

return dialogue.conversation("innkeeper", {
    start = "start",
    topics = {
        room = { "room", "bed" }
    },
    topic_routes = {
        room = "room_offer"
    },
    nodes = {
        start = dialogue.node {
            text = "Welcome.",
            options = {
                dialogue.option { text = "A room", goto_ = "room_offer" }
            }
        },
        room_offer = dialogue.node {
            text = "A room costs 15 gold.",
            options = {}
        }
    }
})
```

Runtime notes:

- `goto_` is normalized to `goto`
- option numbers are selected by nearby player speech like `1`, `2`, `3`
- authored dialogue is meant to run before `ai_dialogue` fallback when both are configured

Persistent dialogue memory is stored per `npc <-> mobile` under:

- `moongate_data/runtime/dialogue_memory/<npc_serial>.json`

Available context helpers include:

- `ctx:get_memory_flag(key)`
- `ctx:set_memory_flag(key, value)`
- `ctx:get_memory_number(key)`
- `ctx:set_memory_number(key, value)`
- `ctx:add_memory_number(key, delta)`
- `ctx:get_memory_text(key)`
- `ctx:set_memory_text(key, value)`

### Scheduled Events

The scheduled event DSL ships as a helper script with runtime support from the `scheduled_events` module.

```lua
local scheduled_events = require("common.scheduled_events")

return scheduled_events.event("town_crier_morning", {
    trigger_name = "town_crier_announcement",
    recurrence = "weekly",
    time = "09:00",
    time_zone = "Europe/Rome",
    days_of_week = { "monday", "wednesday" },
    payload = {
        message = "Hear ye!"
    }
})
```

Supported recurrence values:

- `once`
- `daily`
- `weekly`
- `monthly`

Key fields:

- `enabled`
- `trigger_name`
- `recurrence`
- `time`
- `time_zone`
- `start_at`
- `days_of_week`
- `day_of_month`
- `payload`

When a scheduled event fires, the global script bridge invokes:

```lua
function on_scheduled_event(event)
    if event.trigger_name == "town_crier_announcement" then
        log.info("Scheduled event fired: " .. event.event_id)
    end
end
```

Current payload fields exposed through the event object:

- `event.event_id`
- `event.trigger_name`
- `event.scheduled_at_utc`
- `event.fired_at_utc`
- `event.recurrence_type`
- `event.payload`

### Help Ticketing

The in-game help flow is still entered through `0x9B` and Lua `on_help_request(...)`, but the default gump now drives a ticket wizard:

1. category selection
2. free-text entry
3. ticket persistence
4. `TicketOpenedEvent` publication

Lua submit helpers are exposed through `help_tickets`:

```lua
local help_tickets = require("help_tickets")

help_tickets.submit(session_id, "Question", "I am stuck near Britain bank.")
```

Supported category names:

- `Question`
- `Stuck`
- `Bug`
- `Account`
- `Suggestion`
- `Other`
- `VerbalHarassment`
- `PhysicalHarassment`

Persisted tickets store:

- sender account and character ids
- category
- message
- map id and location
- status (`Open`, `Assigned`, `Closed`)
- timestamps for creation, assignment, closing, and last update

For staff-facing triage, dashboard usage, and endpoint mapping, see
[Help Ticket Workflow](../operations/help-ticket-workflow.md).

### Loot Containers

Loot containers are data-driven through item templates and loot templates rather than a dedicated Lua module.

- container item templates declare `lootTables`
- loot templates define the weighted entries that can be generated
- first-open generation and optional refill behavior are handled by the server when the container is opened

For first-open generation, refillable containers, validation caveats, and real template examples, see
[Loot Containers](loot-containers.md).

### Item Script: Apple

```lua
items_apple = {
    on_double_click = function(ctx)
        local ref = item.get(ctx.item.serial)
        if ref then
            ref:delete()
        end
        speech.send(ctx.session_id, "You eat the apple.")
    end
}
```

### Item Script: Door

```lua
items_door = {
    on_double_click = function(ctx)
        local toggled = door.toggle(ctx.item.serial)
        local ref = item.get(ctx.item.serial)
        if ref then
            if toggled then
                ref:play_sound(0xEA)
            else
                ref:play_sound(0xEC)
            end
        end
    end
}
```

### Item Script: Dye Tub

```lua
items_dye_tub = {
    on_double_click = function(ctx)
        return dye.begin(ctx.session_id, ctx.item.serial, function(target_serial)
            return target_serial ~= nil and target_serial ~= 0
        end)
    end
}
```

The `dye` module currently exposes:

- `dye.begin(session_id, dye_tub_serial, callback?)`
  - starts the classic UO target cursor flow
  - the optional callback receives the selected `target_serial`
  - returning `false` rejects the target before the hue picker opens
- `dye.send_dyeable(session_id, item_serial, model?)`
  - opens the hue picker directly for a known dyeable item

Runtime notes:

- target items must currently be accessible from the player inventory/equipment
- the final hue application, persistence, and item refresh are handled by `IDyeColorService`
- item templates opt-in through the `dyeable` flag, which is materialized into runtime item metadata

### Item Script: Bulletin Board

```lua
items_bulletin_board = {
    on_double_click = function(ctx)
        return bulletin.open(ctx.session_id, ctx.item.serial)
    end
}
```

The `bulletin` module currently exposes:

- `bulletin.open(session_id, board_serial)`
  - opens the classic bulletin board UI for the specified board item
  - the server then sends message summaries over packet `0x71`

Runtime notes:

- the board protocol uses the item serial as `BoardId`
- posting and reply creation are persisted through the bulletin-board message repository
- delete is currently owner-only and rejected when the target message already has replies

### Item Script: Teleporter

```lua
items_teleport = {
    on_double_click = function(ctx)
        local meta = ctx.metadata or {}
        local dest_map = meta.dest_map
        local dest = convert.parse_point3d(meta.dest_x, meta.dest_y, meta.dest_z)
        local delay = convert.parse_delay_ms(meta.delay or "0")

        local mob = mobile.get(ctx.mobile_id)
        if not mob then return end

        local map_id = map.to_id(dest_map or mob.map_id)
        effect.send_to_player(ctx.mobile_id, mob.location_x, mob.location_y, mob.location_z,
            0x3728, 10, 10, 0, 0, 2023)
        mob:play_sound(0x1FE)

        if delay > 0 then
            timer.after(delay, function()
                mob:teleport(map_id, dest.x, dest.y, dest.z)
            end)
        else
            mob:teleport(map_id, dest.x, dest.y, dest.z)
        end
    end
}
```

### Read-Only Book Template

Item templates can point to a book text file with `bookId`. The file lives under
`moongate_data/templates/books/<book_id>.txt`, supports the same `#` comments and
Scriban placeholders used by text templates, and is rendered once when the item
is created.

Example item template:

```json
{
  "type": "item",
  "id": "moongate_welcome_book",
  "name": "Welcome To Moongate",
  "category": "Books",
  "itemId": "0x0FF0",
  "scriptId": "none",
  "bookId": "welcome_player",
  "isMovable": true,
  "tags": ["book", "moongate"]
}
```

Example book file:

```txt
[Title] Welcome To {{ shard.name }}
[Author] The Moongate Team
[ReadOnly] True

Welcome traveler.
Website: {{ shard.website_url }}
```

At runtime the rendered `title`, `author`, and `content` are stored into the item
custom params (`book_title`, `book_author`, `book_content`). Double-click opens
the classic client book UI in read-only mode. The server also listens to client
`0x66` page requests and serves the rendered book content page-by-page.

Writable books use the same classic client UI, but the save flow differs:

- `book_writable = true` marks the item as writable at runtime
- `0xD4` saves `title` and `author`
- `0x66` saves page content
- writes are accepted only when the book is equipped by the player or inside the player's backpack tree

Current writable storage still uses the item custom params:

- `book_title`
- `book_author`
- `book_content`
- `book_writable`

Book templates can also declare writability directly in the `.txt` file:

- `[ReadOnly] True` -> forces the resulting item to be read-only
- `[ReadOnly] False` -> forces the resulting item to be writable
- if `[ReadOnly]` is absent, Moongate falls back to item/startup `writable`

When present, `[ReadOnly]` takes precedence over fallback `writable` metadata.

### Lootable Container Template

Container item templates can also declare weighted loot generation with `lootTables`.
This is meant for chests and similar containers that should populate their contents
from `moongate_data/templates/loot/*.json`.

Example chest template:

```json
{
  "type": "item",
  "id": "loot_test_chest",
  "base_item": "metal_chest",
  "name": "Loot Test Chest",
  "category": "Test Containers",
  "itemId": "0x0E40",
  "scriptId": "items.loot_test_chest",
  "isMovable": true,
  "containerLayoutId": "metal_chest",
  "lootTables": ["loot_test_chest_basic"],
  "params": {
    "loot_refillable": {
      "type": "string",
      "value": "true"
    },
    "loot_refill_seconds": {
      "type": "string",
      "value": "300"
    }
  },
  "tags": ["container", "loot"]
}
```

Example loot table:

```json
{
  "type": "loot",
  "id": "loot_test_chest_basic",
  "name": "Loot Test Chest Basic",
  "category": "Test",
  "rolls": 2,
  "noDropWeight": 0,
  "entries": [
    { "itemTemplateId": "gold", "weight": 5, "amount": 125 },
    { "itemTag": "resources", "weight": 3, "amount": 25 },
    { "itemTemplateId": "lockpick", "weight": 2, "amount": 5 }
  ]
}
```

Entry rules:

- each entry may define exactly one of:
  - `itemTemplateId`
  - `itemId`
  - `itemTag`
- `rolls` defaults to `1`
- `itemTag` resolves a random item template that already carries that tag

Runtime behavior:

- `lootTables` are rolled the first time the container is opened
- each loot table can perform multiple weighted rolls via `rolls`
- generated contents are then persisted like any normal container contents
- if `loot_refillable = true`, the container can refill later
- refill is lazy, not timer-driven:
  the container is marked ready only after it becomes empty, and loot is regenerated on a later open once `loot_refill_seconds` has elapsed
- if the container still has items inside, no refill happens

NPC mobile templates can also declare `lootTables`. When an NPC dies:

- equipped items are moved to the corpse
- backpack contents are moved to the corpse
- configured `lootTables` are generated immediately on the corpse in additive mode

Example mobile snippet:

```json
{
  "type": "mobile",
  "id": "zombie_npc",
  "lootTables": ["undead.low"]
}
```

Runtime custom metadata used internally:

- `item_template_id`
- `loot_generated`
- `loot_refill_ready_at_utc`

### GM Command: Eclipse

```lua
command.register("eclipse", function(ctx)
    weather.set_global_light(26)
    speech.broadcast("The world goes dark...")
end, { gm = true })
```

### NPC Brain: Orion (Cat)

```lua
local MOVE_INTERVAL = 1000
local SPEECH_INTERVAL = 2000
local SOUND_INTERVAL = 3000

local messages = {
    "Meow!", "Purrrr...", "Mrrrow!", "*rubs against your leg*"
}

orion = {}

function orion.on_think(npc_id)
    local mob = mobile.get(npc_id)
    local last_move, last_speech, last_sound = 0, 0, 0

    while true do
        local now = time.now_ms()

        if mob and mob:is_alive() then
            if now - last_move >= MOVE_INTERVAL then
                mob:wander(3)
                last_move = now
            end
            if now - last_speech >= SPEECH_INTERVAL then
                local msg = messages[random.int(1, #messages)]
                mob:say(msg)
                last_speech = now
            end
            if now - last_sound >= SOUND_INTERVAL then
                mob:play_sound(0xDB)
                last_sound = now
            end
        end

        coroutine.yield(250)
    end
end

function orion.on_event(event_type, from_serial, event_obj)
    if event_type == "speech_heard" and event_obj then
        local text = string.lower(event_obj.text or "")
        if string.find(text, "hello", 1, true) then
            local mob = mobile.get(event_obj.listener_npc_id)
            if mob then mob:say("Meow!") end
        end
    end
end
```

## Runtime Notes

Current runtime includes visual effect APIs:

```lua
-- global module
effect.send(mapId, x, y, z, itemId, speed, duration, hue, renderMode, effect, explodeEffect, explodeSound, layer, unknown3)
effect.send_to_player(characterId, x, y, z, itemId, speed, duration, hue, renderMode, effect, explodeEffect, explodeSound, layer, unknown3)

-- mobile proxy
mobile.get(serial):SetEffect(itemId, speed, duration, hue, renderMode, effect, explodeEffect, explodeSound, layer, unknown3)
```

## Mobile Template Skill Materialization

`MobileTemplateDefinition.skills` is now materialized into the persisted mobile
entity at creation time.

Template shape:

```json
{
  "id": "mage_apprentice",
  "type": "mobile",
  "skills": {
    "magery": 750,
    "meditation": 500,
    "wrestling": 300
  }
}
```

Behavior:

- newly created mobiles get a full skill table seeded from `SkillInfo.Table`
- unspecified skills default to `0`
- specified template skills override those defaults
- character creation also creates the full skill table and maps the four starting skills into persisted mobile skills

Current persisted skill entry fields are:

- `value`
- `base`
- `cap`
- `lock`

The runtime `0x3A` skill list packet reads directly from this persisted mobile skill table.

## Global Modules

### log - Logging Module

```lua
log.debug(message: string)      -- Debug level logging
log.info(message: string)       -- Info level logging
log.warning(message: string)    -- Warning level logging
log.error(message: string)      -- Error level logging
log.critical(message: string)   -- Critical level logging
```

**Example:**
```lua
log.debug("Debug information for developers")
log.info("Server started successfully")
log.warning("Low memory warning")
log.error("Failed to load template")
log.critical("Database connection lost")
```

### server - Server Module

```lua
server.broadcast(message: string)                    -- Broadcast to all players
server.get_player_count(): number                    -- Get active player count
server.get_player(serial: number): Player|nil        -- Get player by serial
server.get_players(): table                          -- Get all players
server.shutdown()                                    -- Graceful shutdown
server.save_world()                                  -- Save world state
server.get_uptime(): number                          -- Get server uptime in seconds
server.get_version(): string                         -- Get server version
```

**Example:**
```lua
-- Broadcast message
server.broadcast("Server will restart in 5 minutes!")

-- Get player count
local count = server.get_player_count()
log.info("Players online: " .. count)

-- Get specific player
local player = server.get_player(0x00000001)
if player then
    log.info("Found player: " .. player.Name)
end
```

### game - Game Module

```lua
game.spawn_mobile(body: number, hue: number, x: number, y: number, z: number, map: number): number
game.spawn_item(itemId: number, amount: number, x: number, y: number, z: number): number
game.spawn_npc(npcId: string, x: number, y: number, z: number): number
game.get_mobile(serial: number): Mobile|nil          -- Get mobile by serial
game.get_item(serial: number): Item|nil              -- Get item by serial
game.move_object(serial: number, x: number, y: number, z: number): boolean
game.delete_object(serial: number): boolean          -- Delete object
game.get_distance(obj1: number, obj2: number): number
game.get_objects_in_range(x: number, y: number, z: number, range: number): table
```

**Example:**
```lua
-- Spawn a mobile
local mobileSerial = game.spawn_mobile(0x0190, 0, 1000, 2000, 0, 0)

-- Spawn an item
local itemSerial = game.spawn_item(0x0E76, 1, 1000, 2000, 0)

-- Move object
local success = game.move_object(mobileSerial, 1001, 2000, 0)

-- Get distance
local distance = game.get_distance(playerSerial, targetSerial)
if distance > 10 then
    log.warning("Target too far away")
end
```

### player - Player Module

```lua
player.send_message(serial: number, text: string)              -- Send message to player
player.send_gump(serial: number, gumpId: number, data: table)  -- Send gump dialog
player.teleport(serial: number, x: number, y: number, z: number, map: number)
player.add_item(serial: number, itemId: number, amount: number): number
player.remove_item(serial: number, itemSerial: number, amount: number): boolean
player.get_skill(serial: number, skillName: string): number
player.set_skill(serial: number, skillName: string, value: number)
player.get_stats(serial: number): table                        -- Get str/dex/int
player.set_stats(serial: number, stats: table)                 -- Set str/dex/int
player.send_sound(serial: number, soundId: number)             -- Play sound
player.send_effect(serial: number, effectId: number, target: number)
```

**Example:**
```lua
-- Send message
player.send_message(playerSerial, "Welcome to the server!")

-- Teleport player
player.teleport(playerSerial, 5000, 1000, 0, 0)

-- Add item to backpack
local backpackItem = player.add_item(playerSerial, 0x0E76, 1)

-- Get/Set skills
local magery = player.get_skill(playerSerial, "magery")
player.set_skill(playerSerial, "magery", 100.0)

-- Get stats
local stats = player.get_stats(playerSerial)
log.info("STR: " .. stats.Strength .. ", DEX: " .. stats.Dexterity .. ", INT: " .. stats.Intelligence)
```

### world - World Module

```lua
world.get_time(): table                         -- Get server time {year, month, day, hour, minute, second}
world.get_tile(x: number, y: number, z: number, map: number): table
world.get_region(x: number, y: number, map: number): string
world.get_weather(): table                      -- Get current weather
world.set_weather(weatherType: number, duration: number)
world.spawn_npc(npcId: string, x: number, y: number, z: number, map: number): number
world.despawn(serial: number): boolean
world.is_day(): boolean                         -- Check if it's daytime
world.get_players_in_region(region: string): table
```

**Example:**
```lua
-- Get time
local time = world.get_time()
log.info(string.format("Time: %02d:%02d:%02d", time.hour, time.minute, time.second))

-- Get tile info
local tile = world.get_tile(1000, 2000, 0, 0)
log.info("Tile ID: " .. tile.Id .. ", Z: " .. tile.Z)

-- Get region
local region = world.get_region(1000, 2000, 0)
log.info("Region: " .. region)

-- Set weather
world.set_weather(2, 300)  -- Rain for 5 minutes
```

### commands - Commands Module

```lua
commands.register(name: string, handler: function)           -- Register chat command
commands.unregister(name: string)                            -- Unregister command
commands.process(playerSerial: number, text: string): boolean, any
commands.list(): table                                       -- List all commands
```

**Example:**
```lua
-- Register command
commands.register("teleport", function(playerSerial, args)
    local player = server.get_player(playerSerial)
    if not player.IsAdmin then
        return false, "Access denied"
    end
    
    local x, y, z = args:match("(%d+) (%d+) (%d+)")
    player.teleport(playerSerial, tonumber(x), tonumber(y), tonumber(z))
    return true
end)

-- Unregister command
commands.unregister("teleport")

-- List commands
local cmds = commands.list()
for _, cmd in ipairs(cmds) do
    log.info("Command: " .. cmd)
end
```

## Data Types

### Player

```lua
Player = {
    Serial: number,           -- Unique identifier
    Name: string,             -- Character name
    Account: string,          -- Account username
    Position: Point3D,        -- Current position
    IsAdmin: boolean,         -- Admin flag
    IsModerator: boolean,     -- Moderator flag
    IsInWorld: boolean,       -- In world flag
    LastActivity: number      -- Last activity timestamp
}
```

### Mobile

```lua
Mobile = {
    Serial: number,           -- Unique identifier
    Name: string,             -- Mobile name
    Body: number,             -- Body ID
    Hue: number,              -- Hue color
    Position: Point3D,        -- Current position
    Map: number,              -- Map facet
    Hits: number,             -- Current hits
    HitsMax: number,          -- Maximum hits
    Stamina: number,          -- Current stamina
    StaminaMax: number,       -- Maximum stamina
    Mana: number,             -- Current mana
    ManaMax: number,          -- Maximum mana
    Direction: number,        -- Facing direction
    WarMode: boolean,         -- War mode flag
    Paralyzed: boolean,       -- Paralyzed flag
    Poisoned: boolean         -- Poisoned flag
}
```

### Item

```lua
Item = {
    Serial: number,           -- Unique identifier
    ItemId: number,           -- Item graphic ID
    Amount: number,           -- Stack amount
    Hue: number,              -- Hue color
    Position: Point3D,        -- Position (if world item)
    ParentSerial: number|nil, -- Parent container serial
    Layer: number|nil,        -- Equip layer (if equipped)
    IsMovable: boolean,       -- Can be picked up
    IsContainer: boolean      -- Is a container
}
```

### Point3D

```lua
Point3D = {
    X: number,                -- X coordinate
    Y: number,                -- Y coordinate
    Z: number                 -- Z coordinate
}
```

### Map

```lua
Map = {
    Felucca = 0,              -- Felucca facet
    Trammel = 1,              -- Trammel facet
    Ilshenar = 2,             -- Ilshenar facet
    Malas = 3,                -- Malas facet
    Tokuno = 4,               -- Tokuno facet
    TerMur = 5                -- TerMur facet
}
```

## Callbacks

Moongate currently exposes two different callback styles:

- global script callbacks such as `on_player_connected(...)`, `on_aggressive_action(...)`, and `on_ticket_opened(...)`
- NPC brain callbacks such as `on_event(...)`, `on_speech(...)`, `on_death(...)`

These are not the same thing:

- global callbacks come from the game event bus through `GameEventScriptBridgeService`
- brain callbacks are dispatched to one NPC brain instance at a time

### Server Callbacks

```lua
function on_server_start()              -- Called when server starts
function on_server_stop()               -- Called when server stops
function on_tick()                      -- Called every game tick
function on_save_world()                -- Called before world save
```

### Player Callbacks

```lua
function on_player_connected(player)    -- Player connected
function on_player_disconnected(player) -- Player disconnected
function on_player_speech(player, text) -- Player spoke
function on_player_login(player)        -- Player logged in
function on_player_logout(player)       -- Player logged out
function on_player_use_item(player, item) -- Player used item
function on_player_equip_item(player, item) -- Player equipped item
function on_player_combat_hit(attacker, defender, damage) -- Combat hit
```

### World Callbacks

```lua
function on_mobile_created(mobile)      -- Mobile created
function on_mobile_deleted(mobile)      -- Mobile deleted
function on_item_created(item)          -- Item created
function on_item_deleted(item)          -- Item deleted
function on_weather_changed(weather)    -- Weather changed
```

### Global Game Event Bridge

Some gameplay events are forwarded automatically to Lua global callbacks by naming convention:

```lua
function on_aggressive_action(event)
    log.info(
        "Aggressive action: attacker={0} defender={1}",
        tostring(event.attacker_id),
        tostring(event.defender_id)
    )
end
```

Current behavior:

- event type `AggressiveActionEvent` maps to Lua callback `on_aggressive_action`
- this callback is global script-side, not an NPC brain hook
- it is intended for shard-level reactions such as:
  - guarded region checks
  - no-combat zone logic
  - analytics or logging

Current payload fields for `on_aggressive_action(event)`:

- `attacker_id`
- `defender_id`
- `map_id`
- `location`
- `attacker`
- `defender`

Help tickets also enter the global bridge:

```lua
function on_ticket_opened(event)
    log.info(
        "Ticket opened: id={0} category={1} sender={2}",
        tostring(event.ticket_id),
        tostring(event.category),
        tostring(event.sender_character_id)
    )
end
```

Current behavior:

- event type `TicketOpenedEvent` maps to Lua callback `on_ticket_opened`
- this callback is global script-side, not an NPC brain hook
- it is intended for shard-level reactions such as:
  - staff alerting
  - Discord/webhook bridges
  - analytics or audit trails

Current payload fields for `on_ticket_opened(event)`:

- `ticket_id`
- `sender_character_id`
- `sender_account_id`
- `category`
- `message`
- `map_id`
- `location`
- `created_at_utc`

## Item Script Dispatcher API

Item templates/entities can define a `scriptId`. The server can dispatch item hooks through `IItemScriptDispatcher`.

### Dispatch Convention

```lua
<script_id_normalized>.<hook_name>(ctx)
```

Example:

```lua
-- scriptId: "items.healing-potion" => table "items_healing_potion"
items_healing_potion = {
    on_click = function(ctx)
        log.info("Item hook called for " .. tostring(ctx.item.serial))
    end,
    on_double_click = function(ctx)
        log.info("Double click from session " .. tostring(ctx.session_id))
    end
}
```

Fallback naming when `scriptId == "none"`:

```lua
-- item name "Brick" -> "brick" (first) then "items_brick" (second)
brick = {
    on_double_click = function(ctx)
        log.info("Brick used by session " .. tostring(ctx.session_id))
    end
}
```

Hook aliases:

- `single_click` -> `on_click`, `OnClick`, `on_single_click`, `OnSingleClick`
- `double_click` -> `on_double_click`, `OnDoubleClick`

### Mobile Helper APIs For Item Scripts

Item scripts can use the `mobile` module to bridge skill checks and inventory operations without moving item behavior into C#:

```lua
local mob = mobile.get(character_id)
local weapon = mobile.get_weapon(character_id)
local backpack = mobile.get_backpack(character_id)
local displayed = mobile.get_skill(character_id, "archery")
local success = mobile.check_skill(character_id, "archery", -25.0, 25.0, target_serial)
local consumed = mobile.consume_item(character_id, 0x0F3F, 1)
local added = mobile.add_item_to_backpack(character_id, "arrow", 5)
```

Available helpers:

- `mobile.get(serial)` -> returns a mobile proxy or `nil`
- `mobile.get_skill(serial, skill_name)` -> displayed base skill value as `number`
- `mobile.check_skill(serial, skill_name, min_skill, max_skill, target_serial?)` -> runs a skill check and applies normal gain rules
- `mobile.get_weapon(serial)` -> equipped weapon proxy or `nil`
- `mobile.get_backpack(serial)` -> backpack proxy or `nil`
- `mobile.consume_item(serial, item_id, amount?)` -> consumes from quivers first, then backpack
- `mobile.add_item_to_backpack(serial, template_id, amount?)` -> spawns a template item and moves it into the backpack

These helpers are used by training-style item scripts such as melee dummies, archery buttes, and pickpocket dips.

### Text API

`text.render(...)` loads a Scriban template from `moongate_data/scripts/texts/**` and returns the rendered string.

```lua
local body = text.render("welcome_player.txt", {
    player = {
        name = "Tommy",
        account_type = "Regular",
        character_id = 2
    }
})
```

Available built-in template values:

- `shard.name`
- `shard.website_url`

Template comment rules:

- a line starting with `#` after trim is ignored
- inline `#` starts a comment and truncates the rest of the line
- use `\#` to keep a literal `#`

Example `moongate_data/scripts/texts/welcome_player.txt`:

```txt
# internal comment
Welcome to {{ shard.name }}, {{ player.name }}.

Website: {{ shard.website_url }} # shown to the player
```

### Gump API

```lua
local builder = gump.create()
builder:resize_pic(0, 0, 9200, 280, 150)
builder:text(20, 20, 1152, "First gump")
builder:button(20, 95, 4005, 4007, 1)
gump.send(session_id, builder, character_id, 0xB10C, 120, 80)

gump.on(0xB10C, 1, function(ctx)
    local second = gump.create()
    second:resize_pic(0, 0, 9200, 260, 120)
    second:text(20, 20, 1152, "Second gump")
    gump.send(ctx.session_id, second, ctx.character_id or 0, 0xB10D, 140, 90)
end)
```

`gump.on(...)` callback `ctx` fields:

- `session_id`
- `character_id`
- `gump_id`
- `button_id`
- `serial`
- `switches`
- `text_entries`

File-based layout (recommended):

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
            log.info("Clicked button " .. tostring(cb_ctx.button_id))
        end
    }
}
```

```lua
local layout = require("gumps/test_shop")
local ui_ctx = { name = "Orion", level = 42 }
gump.send_layout(session_id, layout, character_id, 0xB300, 120, 80, ui_ctx)
```

Using `text.render(...)` with `htmlgump`:

```lua
local body = text.render("welcome_player.txt", {
    player = {
        name = "Tommy"
    }
}) or "Welcome."

local g = gump.create()
g:resize_pic(0, 0, 9200, 420, 240)
g:html(20, 20, 380, 180, body, true, true)
gump.send(session_id, g, character_id, 0xB500, 120, 80)
```

Supported file-based element types currently include:

- `page`, `group`
- `background`, `alpha_region`
- `image`, `image_tiled`, `item`
- `label`, `label_cropped`, `html`
- `checkbox`, `radio`
- `text_entry`, `text_entry_limited`
- `tooltip`
- `button`, `button_page`

## `async_job` Module

Named background jobs callable from Lua:

```lua
async_job.run("echo", "req-1", { text = "hello" })
async_job.try_run("echo", "npc:1", "req-2", { text = "world" })
```

Global completion callbacks:

```lua
function on_async_job_result(job_name, request_id, result)
end

function on_async_job_error(job_name, request_id, message)
end
```

Payload and result values must be plain Lua data:

- `nil`
- `boolean`
- `number`
- `string`
- nested tables / arrays of the same

### ItemScriptContext (`ctx` payload)

```lua
ItemScriptContext = {
    hook: string,
    session_id: number|nil,
    mobile_id: number|nil,
    metadata: table<string, any>|nil,
    item: {
        serial: number,
        script_id: string|nil,
        name: string|nil,
        map_id: number,
        item_id: number,
        amount: number,
        hue: number,
        location: { x: number, y: number, z: number }
    }
}
```

## Utility Functions

### String Utilities

```lua
string.split(str: string, delimiter: string): table
string.trim(str: string): string
string.starts_with(str: string, prefix: string): boolean
string.ends_with(str: string, suffix: string): boolean
string.contains(str: string, substr: string): boolean
```

### Table Utilities

```lua
table.contains(tbl: table, value: any): boolean
table.keys(tbl: table): table
table.values(tbl: table): table
table.length(tbl: table): number
table.merge(tbl1: table, tbl2: table): table
```

### Math Utilities

```lua
math.distance(x1: number, y1: number, x2: number, y2: number): number
math.clamp(value: number, min: number, max: number): number
math.lerp(a: number, b: number, t: number): number
math.random_range(min: number, max: number): number
```

## Error Handling

### pcall for Safe Calls

```lua
local success, result = pcall(function()
    return game.spawn_mobile(0x0190, 0, 1000, 2000, 0)
end)

if not success then
    log.error("Failed to spawn mobile: " .. tostring(result))
else
    log.info("Spawned mobile with serial: " .. result)
end
```

### xpcall with Error Handler

```lua
local function error_handler(err)
    log.error("Script error: " .. tostring(err))
    return err
end

local success, result = xpcall(function()
    risky_operation()
end, error_handler)
```

## Best Practices

### Performance

```lua
-- GOOD: Cache function references
local log_info = log.info
local game_spawn = game.spawn_mobile

for i = 1, 10 do
    log_info("Spawning mobile " .. i)
    game_spawn(0x0190, 0, 1000 + i, 2000, 0)
end

-- BAD: Repeated lookups
for i = 1, 10 do
    log.info("Spawning mobile " .. i)
    game.spawn_mobile(0x0190, 0, 1000 + i, 2000, 0)
end
```

### Memory Management

```lua
-- GOOD: Clear tables when done
local large_table = {}
for i = 1, 10000 do
    large_table[i] = {data = i}
end

-- Process table
process_data(large_table)

-- Clear for GC
large_table = nil

-- BAD: Memory leak
local persistent_table = {}
function on_tick()
    persistent_table[#persistent_table + 1] = {tick = world.get_time()}
end
```

## Next Steps

- **[Modules](modules.md)** - Create custom modules
- **[Overview](overview.md)** - Scripting introduction
- **[Persistence](../persistence/overview.md)** - Data storage

---

**Previous**: [Modules](modules.md) | **Next**: [Persistence Overview](../persistence/overview.md)
