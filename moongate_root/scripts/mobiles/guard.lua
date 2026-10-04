-- ==============================================================================
-- Moongate - scripts/mobiles/guard.lua
--
-- What it is for:
--   A mobile script for the guards of the towns, the ones that stand there by
--   their spawn and the ones a player calls by saying "guards". The server has no
--   combat yet, so a guard only shows itself: it does no harm. A mobile template
--   uses it with script_id = "guard" (guard, m_guard and f_guard do).
--
--   The guard is in one of two states:
--     post    it strolls around its post, the area of its spawn region. Every
--             second it looks for a criminal: a player whose name is grey, within
--             12 tiles and in sight, standing in a guarded region.
--     arrest  it saw one: it goes into war mode, appears on it with the teleport
--             effect and sound when it is not beside it, and says its line. Then
--             it stays on it, running after it when it moves. When the criminal
--             is pardoned, hides, leaves the guarded region or goes farther than
--             24 tiles, the guard goes back to peace and walks back to its post.
--   A guard that was called (the prop guard.summoned) came for its criminal and
--   said its line already: it says nothing more and does not stroll.
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

-- In thinks, two a second: how often it looks for a criminal.
local SCAN_EVERY = 2

-- How far apart two heights of one storey are.
local STOREY = 16

-- The teleport of a guard, as ModernUO's.
local TELEPORT_SOUND = 0x1FE

-- What a guard says to a criminal, in data/messages.
local LINE = { id = 30138, english = "Thou wilt regret thine actions, swine!" }

-- What each guard is doing, by serial.
local minds = {}

local function mind_of(serial)
    local mind = minds[serial]

    if mind == nil then
        mind = { state = "post", thinks = 0, summoned = npc.get_prop(serial, "guard.summoned") == true }
        minds[serial] = mind

        -- A guard the script lost track of, as after a script reload, may still be in war mode.
        local flags = mobile.flags(serial)

        if flags ~= nil and flags.war_mode then
            mobile.set_war_mode(serial, false)
        end
    end

    return mind
end

-- Whether the guards of the place reach the mobile: a criminal that stands in a guarded region.
local function is_wanted(who)
    if not mobile.criminal(who) then
        return false
    end

    local there = mobile.location(who)

    return there ~= nil and world.is_guarded(there.map, there.x, there.y, there.z)
end

-- The nearest criminal the guard sees, or nil.
local function look_for_criminal(serial)
    for _, player in ipairs(npc.players_in_sight(serial, SIGHT)) do
        if is_wanted(player) then
            return player
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
    mobile.set_war_mode(serial, true)

    local there = mobile.location(criminal)

    -- Not beside it: the guard is gone from where it stood and stands on the criminal, as ModernUO's.
    if npc.distance_to(serial, there.x, there.y) > 1 or math.abs(there.z - here.z) > STOREY then
        puff(here)
        mobile.teleport(serial, there.x, there.y, there.z)
        puff(there)
        npc.play_sound(serial, TELEPORT_SOUND)
    end

    -- A guard that was called said it when it came.
    if not mind.summoned then
        npc.say(serial, localization.text(LINE.id) or LINE.english)
    end
end

local function back_to_post(serial, mind)
    mind.state = "post"
    mind.target = nil
    mobile.set_war_mode(serial, false)
end

local function post(serial, mind, here)
    if mind.thinks % SCAN_EVERY == 0 then
        local criminal = look_for_criminal(serial)

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
    if not npc.can_see(serial, criminal, LEASH, false) or not is_wanted(criminal) then
        back_to_post(serial, mind)

        return
    end

    local there = mobile.location(criminal)

    if npc.distance_to(serial, there.x, there.y) <= 1 and math.abs(there.z - here.z) <= STOREY then
        npc.face(serial, there.x, there.y)

        return
    end

    npc.walk_to(serial, there.x, there.y, there.z, 1, true)
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
