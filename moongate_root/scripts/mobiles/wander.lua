-- ==============================================================================
-- Moongate - scripts/mobiles/wander.lua
--
-- What it is for:
--   A mobile script: the NPC announces itself when spawned, wanders around while
--   a player is near, calls out to the players who come close and answers a
--   greeting. An NPC of a spawn region (props spawn.x1, spawn.y1, spawn.x2,
--   spawn.y2) wanders only inside its home area, and walks back to it when outside. A mobile template uses it with script_id = "wander"; the file is
--   named after its script_id and defines the global table of the same name.
--
-- Functions:
--   on_think(serial)                 every think of an NPC near a player
--                                    (ultima.npcs.think_interval_ms); must not
--                                    call wait()
--   on_speech(serial, speaker, text) a player says text within 15 cells; may
--                                    call wait()
--   on_spawn(serial)                 once, right after a spawn, before anything
--                                    else; may call wait()
--   on_mobile_in_range(serial, other)
--                                    a player or NPC comes within
--                                    ultima.npcs.sense_range cells; may call wait()
-- ==============================================================================

wander = {}

-- Thinks per NPC, keyed by serial: a think comes every 500 ms (ultima.npcs.think_interval_ms),
-- so a step every 4th think is one step about every 2 seconds.
local thinks = {}

-- The cell a step in each direction leads to: 0 north, 1 north-east, ... 7 north-west.
local dx = { [0] = 0, 1, 1, 1, 0, -1, -1, -1 }
local dy = { [0] = -1, -1, 0, 1, 1, 1, 0, -1 }

-- The home area of an NPC of a spawn region, or nil for any other NPC.
local function home_of(serial)
    local x1 = npc.get_prop(serial, "spawn.x1")

    if x1 == nil then
        return nil
    end

    return {
        x1 = x1,
        y1 = npc.get_prop(serial, "spawn.y1"),
        x2 = npc.get_prop(serial, "spawn.x2"),
        y2 = npc.get_prop(serial, "spawn.y2")
    }
end

local function inside(home, x, y)
    return x >= home.x1 and x <= home.x2 and y >= home.y1 and y <= home.y2
end

local function sign(value)
    if value > 0 then
        return 1
    elseif value < 0 then
        return -1
    end

    return 0
end

-- A random direction that stays in the home area, nil when none does; outside it, the direction towards it.
local function pick_direction(serial)
    local first = dice.roll("1d8") - 1
    local home = home_of(serial)
    local here = home and npc.location(serial)

    if here == nil then
        return first
    end

    if not inside(home, here.x, here.y) then
        local x = sign(math.max(home.x1 - here.x, 0) + math.min(home.x2 - here.x, 0))
        local y = sign(math.max(home.y1 - here.y, 0) + math.min(home.y2 - here.y, 0))

        for direction = 0, 7 do
            if dx[direction] == x and dy[direction] == y then
                return direction
            end
        end
    end

    for i = 0, 7 do
        local direction = (first + i) % 8

        if inside(home, here.x + dx[direction], here.y + dy[direction]) then
            return direction
        end
    end

    return nil
end

-- Called on every think of an NPC near a player. It must not call wait().
function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        local direction = pick_direction(serial)

        if direction ~= nil then
            npc.step(serial, direction)
        end
    end
end

-- Called when a player says something within 15 cells. It may call wait().
function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        wait(1)
        npc.say(serial, "Well met, traveller.")
    end
end

-- Called once, right after the NPC is spawned. It may call wait().
function wander.on_spawn(serial)
    npc.say(serial, "*stretches*")
end

-- Called when a player or an NPC comes within ultima.npcs.sense_range cells. It may call wait().
-- npc.name is nil for a player: only the players are greeted.
function wander.on_mobile_in_range(serial, other)
    if npc.name(other) == nil then
        npc.say(serial, "Who goes there?")
    end
end
