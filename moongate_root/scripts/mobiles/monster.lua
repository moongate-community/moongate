-- ==============================================================================
-- Moongate - scripts/mobiles/monster.lua
--
-- What it is for:
--   A mobile script for the monsters that go for the players, as ModernUO's melee
--   AI without the fight: the server has no combat yet. A mobile template uses it
--   with script_id = "monster" (the skeleton and the zombie do).
--
--   The monster is in one of three states:
--     wander  it strolls around its home, the area of its spawn region, mostly
--             straight ahead, and now and then rests for 15 to 25 seconds with
--             its idle sound and a fidget.
--     chase   it saw a player within 16 tiles, in line of sight: it threatens it
--             with its attack sound, goes into war mode and walks to it, around
--             what stands in the way, never running. Beside the player it faces
--             it and snarls every few seconds. It does no harm.
--     guard   it lost the player: hidden, gone, farther than 32 tiles or out of
--             reach for 20 seconds. It stands in war mode for 10 seconds, looking
--             around, then goes back to wandering, and so to its home. A
--             player it could not reach is left alone until it moves.
--   It never sees a hidden player, a game master or an administrator, and it
--   ignores the other monsters.
--
-- Functions:
--   on_think(serial)                 every think of an NPC near a player
--                                    (ultima.npcs.think_interval_ms, 500 ms);
--                                    must not call wait()
--
-- What it keeps:
--   Its state is kept in memory by serial, not saved: after a restart, or once no
--   player is near, a monster starts again from wandering.
-- ==============================================================================

monster = {}

-- How far a monster sees a player, and how far it follows one before it gives up (ModernUO: 16 and twice that).
local PERCEPTION = 16
local LEASH = 32

-- In thinks, two a second: how often it looks for a player, how long it stands guard, how long it tries a player it
-- cannot reach, how often it snarls beside one, and how long a rest lasts.
local SCAN_EVERY = 4
local GUARD_THINKS = 20
local GIVE_UP_THINKS = 40
local SNARL_EVERY = 6
local REST_MIN, REST_MAX = 30, 50

-- The eye of a mobile above its feet, for the line of sight, and how far apart two heights of one storey are.
local EYE = 14
local STOREY = 16

-- A monster body's actions: it threatens, attacks and fidgets (ModernUO's choices).
local THREATEN = MonsterAnimationType.Pillage
local ATTACKS = { MonsterAnimationType.Attack1, MonsterAnimationType.Attack2, MonsterAnimationType.Attack3 }
local FIDGETS = { MonsterAnimationType.Fidget1, MonsterAnimationType.Fidget2 }

-- The cell a step in each direction leads to: 0 north, 1 north-east, ... 7 north-west.
local dx = { [0] = 0, 1, 1, 1, 0, -1, -1, -1 }
local dy = { [0] = -1, -1, 0, 1, 1, 1, 0, -1 }

-- What each monster is doing, by serial.
local minds = {}

local function mind_of(serial)
    local mind = minds[serial]

    if mind == nil then
        mind = { state = "wander", thinks = 0, rest = 0, until_think = 0, stalled = 0 }
        minds[serial] = mind

        -- A monster the script lost track of, as after a script reload, may still be in war mode.
        local flags = mobile.flags(serial)

        if flags ~= nil and flags.war_mode then
            mobile.set_war_mode(serial, false)
        end
    end

    return mind
end

local function random(max)
    return dice.roll("1d" .. max)
end

-- The home area of a monster of a spawn region, or nil for any other.
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

-- Whether the monster may go for this player: in the world, on its map, not hidden, not staff, near enough and, when
-- asked, in line of sight.
local function is_prey(here, player, range, in_sight)
    local there = mobile.location(player)

    if there == nil or there.map ~= here.map or world.is_staff(player) then
        return false
    end

    local flags = mobile.flags(player)

    if flags == nil or flags.hidden then
        return false
    end

    if math.max(math.abs(there.x - here.x), math.abs(there.y - here.y)) > range then
        return false
    end

    -- A height is 127 at most.
    return not in_sight or world.line_of_sight(
        here.map, here.x, here.y, math.min(here.z + EYE, 127), there.x, there.y, math.min(there.z + EYE, 127)
    )
end

-- Whether the monster gave this player up as out of reach and the player still stands where it was then.
local function is_given_up(mind, player)
    local given_up = mind.given_up

    if given_up == nil or given_up.player ~= player then
        return false
    end

    local there = mobile.location(player)

    return there ~= nil and there.x == given_up.x and there.y == given_up.y
end

-- The nearest player the monster sees, or nil. One it could not reach is left alone until it moves.
local function look_for_prey(serial, mind, here)
    for _, player in ipairs(npc.nearby(serial, PERCEPTION, "players")) do
        if is_prey(here, player, PERCEPTION, true) and not is_given_up(mind, player) then
            return player
        end
    end

    return nil
end

local function start_chase(serial, mind, player)
    mind.state = "chase"
    mind.target = player
    mind.stalled = 0
    mind.rest = 0
    mind.given_up = nil
    mobile.set_war_mode(serial, true)
    npc.play_sound(serial, "start_attack")
    mobile.animate(serial, THREATEN)
end

local function start_guard(serial, mind)
    mind.state = "guard"
    mind.target = nil
    mind.until_think = mind.thinks + GUARD_THINKS
end

local function start_wander(serial, mind)
    mind.state = "wander"
    mind.target = nil
    mobile.set_war_mode(serial, false)
end

-- One stroll step: mostly straight ahead, kept in the home area, back towards it from outside.
local function stroll(serial, here)
    local home = home_of(serial)
    local facing = (mobile.direction(serial) or 0) % 8
    local first = facing

    -- One time in three it turns somewhere else.
    if random(3) == 1 then
        first = random(8) - 1
    end

    if home == nil then
        npc.step(serial, first)

        return
    end

    -- Outside, as after a chase: back to the nearest cell of home, around what stands in the way. With no way
    -- found it takes a step at random, so a wall between it and home does not hold it for ever.
    if not inside(home, here.x, here.y) then
        local x = math.min(math.max(here.x, home.x1), home.x2)
        local y = math.min(math.max(here.y, home.y1), home.y2)

        if npc.walk_to(serial, x, y) ~= "moving" then
            npc.step(serial, random(8) - 1)
        end

        return
    end

    for i = 0, 7 do
        local direction = (first + i) % 8

        if inside(home, here.x + dx[direction], here.y + dy[direction]) then
            npc.step(serial, direction)

            return
        end
    end
end

local function wander(serial, mind, here)
    if mind.thinks % SCAN_EVERY == 0 then
        local prey = look_for_prey(serial, mind, here)

        if prey ~= nil then
            start_chase(serial, mind, prey)

            return
        end
    end

    if mind.rest > 0 then
        mind.rest = mind.rest - 1

        return
    end

    -- One think in twenty it rests, with its idle sound and a fidget.
    if random(20) == 1 then
        mind.rest = REST_MIN + random(REST_MAX - REST_MIN + 1) - 1
        npc.play_sound(serial, "idle")
        mobile.animate(serial, FIDGETS[random(#FIDGETS)])

        return
    end

    -- A step one think in four, never two thinks in a row: about one every two seconds.
    if mind.thinks % 2 == 0 and random(2) == 1 then
        stroll(serial, here)
    end
end

local function chase(serial, mind, here)
    local target = mind.target

    if not is_prey(here, target, LEASH, false) then
        start_guard(serial, mind)

        return
    end

    local there = mobile.location(target)

    -- Beside it, and on its storey: one tile away on the floor above is not reached.
    if math.max(math.abs(there.x - here.x), math.abs(there.y - here.y)) <= 1 and math.abs(there.z - here.z) <= STOREY then
        mind.stalled = 0
        npc.face(serial, there.x, there.y)

        -- Beside its prey it can only snarl: the fight comes with the combat.
        if mind.thinks % SNARL_EVERY == 0 then
            npc.play_sound(serial, "attack")
            mobile.animate(serial, ATTACKS[random(#ATTACKS)])
        end

        return
    end

    if npc.walk_to(serial, there.x, there.y, there.z, 1) == "moving" then
        mind.stalled = 0

        return
    end

    mind.stalled = mind.stalled + 1

    if mind.stalled >= GIVE_UP_THINKS then
        start_guard(serial, mind)
        mind.given_up = { player = target, x = there.x, y = there.y }
    end
end

local function guard(serial, mind, here)
    if mind.thinks % 2 == 0 then
        local prey = look_for_prey(serial, mind, here)

        if prey ~= nil then
            start_chase(serial, mind, prey)

            return
        end
    end

    if mind.thinks >= mind.until_think then
        start_wander(serial, mind)

        return
    end

    -- One think in eight it looks another way.
    if random(8) == 1 then
        local direction = random(8) - 1
        npc.face(serial, here.x + dx[direction], here.y + dy[direction])
    end
end

-- Called on every think of an NPC near a player. It must not call wait().
function monster.on_think(serial)
    local here = npc.location(serial)

    if here == nil then
        minds[serial] = nil

        return
    end

    local mind = mind_of(serial)
    mind.thinks = mind.thinks + 1

    if mind.state == "chase" then
        chase(serial, mind, here)
    elseif mind.state == "guard" then
        guard(serial, mind, here)
    else
        wander(serial, mind, here)
    end
end
