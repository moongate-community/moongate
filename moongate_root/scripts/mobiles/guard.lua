-- ==============================================================================
-- Moongate - scripts/mobiles/guard.lua
--
-- What it is for:
--   A mobile script for the guards of the towns, the ones that stand there by
--   their spawn and the ones a player calls by saying "guards". The server has no
--   combat yet: a guard kills a criminal that is an NPC with one blow, as
--   ModernUO's does, and only stands on one that is a player, since players do
--   not die yet. A mobile template uses it with script_id = "guard" (guard,
--   m_guard and f_guard do).
--
--   The guard is in one of two states:
--     post    it strolls around its post, the area of its spawn region. Every
--             second it looks for a criminal: a player or an NPC whose name is grey, or a player whose name is red, within
--             12 tiles and in sight, standing in a guarded region no farther than
--             24 tiles from the post.
--     arrest  it saw one: it goes into war mode, appears beside it with the teleport
--             effect and sound when it is not beside it, and says its line. Then
--             it stays on it, running after it when it moves. Beside an NPC it
--             strikes, and a second later the NPC is dead (mobile.kill, with
--             the guard as its killer and its corpse left there); the guard
--             goes back to its post. When the criminal
--             is pardoned, hides, leaves the guarded region, goes farther than 24
--             tiles from the guard or from its post, or cannot be reached for 10
--             seconds, the guard goes back to peace and walks back to its post. A
--             criminal it could not reach is left alone until it moves.
--   A guard that was called (the prop guard.summoned) came for one criminal and
--   said its line already: it says nothing more, does not stroll, and once that
--   criminal is let go it arrests no other.
--   It never sees a hidden player, a game master or an administrator.
--
-- Functions:
--   on_think(serial)   every think of an NPC near a player
--                      (ultima.npcs.think_interval_ms, 500 ms); must not call
--                      wait()
--
-- What it keeps:
--   Its state is kept in memory by serial, not saved: after a restart, or once no
--   player is near, a guard starts again from its post.
-- ==============================================================================

guard = {}

-- How far a guard notices a criminal, and how far it follows one before it lets it go.
local SIGHT = 12
local LEASH = 24

-- In thinks, two a second: how often it looks for a criminal, and how long it tries one it cannot reach.
local SCAN_EVERY = 2
local GIVE_UP_THINKS = 20

-- How far apart two heights of one storey are.
local STOREY = 16

-- How a guard strikes, and how many thinks pass between the blow and the death: a second.
local STRIKE = HumanAnimationType.AttackSlash1H
local STRIKE_THINKS = 2

-- The teleport of a guard, as ModernUO's.
local TELEPORT_SOUND = 0x1FE

-- What a guard says to a criminal, in data/messages.
local LINE = { id = 30138, english = "Thou wilt regret thine actions, swine!" }

-- What each guard is doing, by serial.
local minds = {}

local function mind_of(serial)
    local mind = minds[serial]

    if mind == nil then
        mind = { state = "post", thinks = 0, stalled = 0, summoned = npc.get_prop(serial, "guard.summoned") == true }
        minds[serial] = mind

        -- A guard the script lost track of, as after a script reload, may still be in war mode.
        local flags = mobile.flags(serial)

        if flags ~= nil and flags.war_mode then
            mobile.set_war_mode(serial, false)
        end
    end

    return mind
end

-- How far a spot is from the guard's post, the area of its spawn region; 0 inside it, and for a guard without one.
local function from_post(serial, x, y)
    local home = npc.home(serial)

    if home == nil then
        return 0
    end

    return math.max(home.x1 - x, x - home.x2, home.y1 - y, y - home.y2, 0)
end

-- Whether the guard reaches the mobile: a criminal, or a murderer with its red name, that stands in a guarded region, not
-- too far from its post. So a criminal cannot lead it away step by step.
local function is_wanted(serial, who)
    -- A ghost is no one the guards want: its crimes died with it.
    if mobile.is_dead(who) or not (mobile.criminal(who) or mobile.is_murderer(who)) then
        return false
    end

    local there = mobile.location(who)

    return there ~= nil
        and world.is_guarded(there.map, there.x, there.y, there.z)
        and from_post(serial, there.x, there.y) <= LEASH
end

-- Whether the guard gave this criminal up as out of reach and it still stands where it was then.
local function is_given_up(mind, who)
    local given_up = mind.given_up

    if given_up == nil or given_up.who ~= who then
        return false
    end

    local there = mobile.location(who)

    return there ~= nil and there.x == given_up.x and there.y == given_up.y
end

-- The nearest criminal the guard sees, player or NPC, or nil. Those around are asked what they are first: only a
-- criminal costs a look along the line of sight. One it has just killed is still there while it falls: not again.
local function look_for_criminal(serial, mind)
    for _, who in ipairs(npc.nearby(serial, SIGHT, "all")) do
        if who ~= mind.killed
            and is_wanted(serial, who)
            and not is_given_up(mind, who)
            and npc.can_see(serial, who, SIGHT) then
            return who
        end
    end

    return nil
end

local function puff(where)
    effect.at(where.map, where.x, where.y, where.z, EffectGraphicType.Smoke)
end

local function start_arrest(serial, mind, here, criminal)
    mind.state = "arrest"
    mind.target = criminal
    mind.strike = nil
    mind.stalled = 0
    mind.given_up = nil
    mobile.set_war_mode(serial, true)

    local there = mobile.location(criminal)

    -- Not beside it: the guard is gone from where it stood and stands beside the criminal, on a free tile a step
    -- from it, or on it when there is none. A teleport that is refused leaves it where it is, to run there.
    if npc.distance_to(serial, there.x, there.y) > 1 or math.abs(there.z - here.z) > STOREY then
        local spot = world.spot_beside(there.map, there.x, there.y, there.z) or there

        if mobile.teleport(serial, spot.x, spot.y, spot.z) then
            puff(here)
            puff({ map = there.map, x = spot.x, y = spot.y, z = spot.z })
            npc.play_sound(serial, TELEPORT_SOUND)
        end
    end

    -- A guard that was called said it when it came.
    if not mind.summoned then
        npc.say(serial, localization.text(LINE.id) or LINE.english)
    end
end

local function back_to_post(serial, mind)
    mind.state = "post"
    mind.target = nil
    -- A guard that was called came for this one.
    mind.done = true
    mobile.set_war_mode(serial, false)
end

local function post(serial, mind, here)
    -- A guard that was called had one criminal: it waits to be sent away.
    if mind.summoned and mind.done then
        return
    end

    if mind.thinks % SCAN_EVERY == 0 then
        local criminal = look_for_criminal(serial, mind)

        if criminal ~= nil then
            start_arrest(serial, mind, here, criminal)

            return
        end
    end

    -- A step one think in eight, never two thinks in a row: about one every four seconds. From outside its post it
    -- walks back. A guard that was called has no post and stands where it came.
    if not mind.summoned and mind.thinks % 2 == 1 and math.random(4) == 1 then
        npc.wander(serial)
    end
end

local function arrest(serial, mind, here)
    local criminal = mind.target

    -- It follows what it saw without seeing it, up to the leash: not one that hid, left or was pardoned.
    if not npc.can_see(serial, criminal, LEASH, false) or not is_wanted(serial, criminal) then
        back_to_post(serial, mind)

        return
    end

    local there = mobile.location(criminal)

    if npc.distance_to(serial, there.x, there.y) <= 1 and math.abs(there.z - here.z) <= STOREY then
        mind.stalled = 0
        npc.face(serial, there.x, there.y)

        -- A player is only stood on: players do not die yet.
        if mobile.is_player(criminal) then
            return
        end

        -- The blow, then the death a second later.
        if mind.strike == nil then
            mind.strike = mind.thinks
            mobile.animate(serial, STRIKE)
        elseif mind.thinks - mind.strike >= STRIKE_THINKS then
            mobile.kill(criminal, serial)
            -- Dead or not, it is done with this one: it falls for a moment and is still a criminal meanwhile.
            mind.killed = criminal
            back_to_post(serial, mind)
        end

        return
    end

    -- It got away from the blow.
    mind.strike = nil

    if npc.walk_to(serial, there.x, there.y, there.z, 1, true) == "moving" then
        mind.stalled = 0

        return
    end

    mind.stalled = mind.stalled + 1

    if mind.stalled >= GIVE_UP_THINKS then
        back_to_post(serial, mind)
        mind.given_up = { who = criminal, x = there.x, y = there.y }
    end
end

-- Called on every think of an NPC near a player. It must not call wait().
function guard.on_think(serial)
    local here = npc.location(serial)

    if here == nil then
        minds[serial] = nil

        return
    end

    local mind = mind_of(serial)
    mind.thinks = mind.thinks + 1

    if mind.state == "arrest" then
        arrest(serial, mind, here)
    else
        post(serial, mind, here)
    end
end
