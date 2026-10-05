-- ==============================================================================
-- Moongate - scripts/mobiles/wander.lua
--
-- What it is for:
--   A mobile script: the NPC announces itself when spawned, wanders around while
--   a player is near, calls out to the players who come close and answers a
--   greeting. It strolls with npc.wander: an NPC of a spawn region keeps to its
--   home area and walks back to it from outside. A mobile template uses it with
--   script_id = "wander"; the file is named after its script_id and defines the
--   global table of the same name.
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

-- Called on every think of an NPC near a player. It must not call wait().
function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        npc.wander(serial)
    end
end

-- Called when a player says something within 15 cells. It may call wait().
function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        npc.look_at(serial, speaker)
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
