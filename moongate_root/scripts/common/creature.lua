-- ==============================================================================
-- Moongate - scripts/common/creature.lua
--
-- What it is for:
--   What the mobile scripts of the creatures share: the monsters that go for the
--   players (mobiles/monster.lua), the animals that keep to themselves
--   (mobiles/animal.lua) and those that run from a blow (mobiles/scared_animal.lua).
--   A script takes it with local creature = require("common.creature") and makes
--   its own table with creature.new(options).
--
-- Options:
--   hunts   true: it goes for the players it sees (ModernUO's melee AI); false: it
--           never starts a fight, it only answers one
--   flees   true: it does not fight back, it runs from who hit it
--
-- Functions:
--   creature.new(options)  gives a table with on_think(serial), the function a
--                          mobile script defines
--
-- What a creature keeps:
--   Its state in memory by serial, not saved: after a restart, or once no player
--   is near, it starts again from wandering.
-- ==============================================================================

local creature = {}

-- How far a creature runs from who hit it, in cells, and for how many thinks (ten seconds).
local FLEE_DISTANCE = 12
local FLEE_THINKS = 20

-- How far a creature sees a player, and how far it follows one before it gives up (ModernUO: 16 and twice that).
local PERCEPTION = 16
local LEASH = 32

-- In thinks, two a second: how often it looks for a player, how long it stands guard, how long it tries a player it
-- cannot reach, and how long a rest lasts.
local SCAN_EVERY = 4
local GUARD_THINKS = 20
local GIVE_UP_THINKS = 40
local REST_MIN, REST_MAX = 30, 50

-- How far apart two heights of one storey are.
local STOREY = 16

-- A creature body's actions: it threatens and fidgets (ModernUO's choices); its attacks are the combat service's.
local THREATEN = MonsterAnimationType.Pillage
local FIDGETS = { MonsterAnimationType.Fidget1, MonsterAnimationType.Fidget2 }

-- What each creature is doing, by serial.
local minds = {}

local function mind_of(serial)
    local mind = minds[serial]

    if mind == nil then
        mind = { state = "wander", thinks = 0, rest = 0, until_think = 0, stalled = 0 }
        minds[serial] = mind

        -- A creature the script lost track of, as after a script reload, may still be in war mode.
        local flags = mobile.flags(serial)

        if flags ~= nil and flags.war_mode then
            mobile.set_war_mode(serial, false)
        end
    end

    return mind
end

-- Whether the creature gave this player up as out of reach and the player still stands where it was then.
local function is_given_up(mind, player)
    local given_up = mind.given_up

    if given_up == nil or given_up.player ~= player then
        return false
    end

    local there = mobile.location(player)

    return there ~= nil and there.x == given_up.x and there.y == given_up.y
end

-- The nearest player the creature sees, or nil. One it could not reach is left alone until it moves: the two
-- nearest are asked for, so the next one is taken then.
local function look_for_prey(serial, mind, hunts)
    if not hunts then
        return nil
    end

    for _, player in ipairs(npc.players_in_sight(serial, PERCEPTION, 2)) do
        if not is_given_up(mind, player) then
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

-- Someone hit it, or missed it, and the combat service made it fight back: it turns on that one, whatever it was doing,
-- and without threatening it, since the fight has begun.
local function retaliate(serial, mind, attacker)
    mind.state = "chase"
    mind.target = attacker
    mind.stalled = 0
    mind.rest = 0
    mind.given_up = nil
    mobile.set_war_mode(serial, true)
end

local function start_guard(serial, mind)
    combat.stop(serial)
    mind.state = "guard"
    mind.target = nil
    mind.until_think = mind.thinks + GUARD_THINKS
end

local function start_wander(serial, mind)
    combat.stop(serial)
    mind.state = "wander"
    mind.target = nil
    mobile.set_war_mode(serial, false)
end

-- Someone hit it and it runs: it stops fighting and walks away from the one who did, running, until it is far enough or
-- ten seconds have passed.
local function start_flee(serial, mind, attacker)
    combat.stop(serial)
    mind.state = "flee"
    mind.target = attacker
    mind.until_think = mind.thinks + FLEE_THINKS
    mobile.set_war_mode(serial, false)
end

local function flee(serial, mind, here)
    -- The combat service makes whoever is hit answer the blow: a creature that runs never does.
    combat.stop(serial)

    local from = mobile.location(mind.target)

    if from == nil or mind.thinks >= mind.until_think or npc.distance_to(serial, from.x, from.y) >= FLEE_DISTANCE then
        start_wander(serial, mind)

        return
    end

    -- The point on the far side of the creature from the one it runs from.
    local away_x = here.x + (here.x >= from.x and FLEE_DISTANCE or -FLEE_DISTANCE)
    local away_y = here.y + (here.y >= from.y and FLEE_DISTANCE or -FLEE_DISTANCE)

    npc.walk_to(serial, away_x, away_y, here.z, 0, true)
end

local function wander(serial, mind, hunts)
    if mind.thinks % SCAN_EVERY == 0 then
        local prey = look_for_prey(serial, mind, hunts)

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
    if math.random(20) == 1 then
        mind.rest = math.random(REST_MIN, REST_MAX)
        npc.play_sound(serial, "idle")
        mobile.animate(serial, FIDGETS[math.random(#FIDGETS)])

        return
    end

    -- A step one think in four, never two thinks in a row: about one every two seconds.
    if mind.thinks % 2 == 0 and math.random(2) == 1 then
        npc.wander(serial)
    end
end

local function chase(serial, mind, here)
    local target = mind.target

    -- It follows what it saw without seeing it, up to the leash: not one that hid or left.
    if not npc.can_see(serial, target, LEASH, false) then
        start_guard(serial, mind)

        return
    end

    local there = mobile.location(target)

    -- Beside it, and on its storey: one tile away on the floor above is not reached.
    if npc.distance_to(serial, there.x, there.y) <= 1 and math.abs(there.z - here.z) <= STOREY then
        mind.stalled = 0
        npc.face(serial, there.x, there.y)

        -- Beside its prey it fights it, once: the swings are the combat service's.
        if combat.target(serial) ~= target then
            combat.attack(serial, target)
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

-- The cell a step in each direction leads to: 0 north, 1 north-east, ... 7 north-west.
local dx = { [0] = 0, 1, 1, 1, 0, -1, -1, -1 }
local dy = { [0] = -1, -1, 0, 1, 1, 1, 0, -1 }

local function guard(serial, mind, here, hunts)
    if mind.thinks % 2 == 0 then
        local prey = look_for_prey(serial, mind, hunts)

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
    if math.random(8) == 1 then
        local direction = math.random(0, 7)
        npc.face(serial, here.x + dx[direction], here.y + dy[direction])
    end
end

-- Makes the table of a creature script, with its on_think.
function creature.new(options)
    local hunts = options.hunts == true
    local flees = options.flees == true
    local script = {}

    -- Called on every think of an NPC near a player. It must not call wait().
    function script.on_think(serial)
        local here = npc.location(serial)

        if here == nil then
            minds[serial] = nil

            return
        end

        local mind = mind_of(serial)
        mind.thinks = mind.thinks + 1

        -- Whoever it fights is the one it chases, even if it has not seen it: a creature that is hit does not go on
        -- strolling. One that runs does not fight: the combat service made it answer the blow, and it is told to stop.
        local fought = combat.target(serial)

        if fought ~= nil and (mind.state ~= "chase" or mind.target ~= fought) and (mind.state ~= "flee" or mind.target ~= fought) then
            if flees then
                start_flee(serial, mind, fought)
            else
                retaliate(serial, mind, fought)
            end
        end

        if mind.state == "flee" then
            flee(serial, mind, here)
        elseif mind.state == "chase" then
            chase(serial, mind, here)
        elseif mind.state == "guard" then
            guard(serial, mind, here, hunts)
        else
            wander(serial, mind, hunts)
        end
    end

    return script
end

return creature
