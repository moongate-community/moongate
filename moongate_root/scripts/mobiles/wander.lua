-- A mobile script: a template uses it with script_id = "wander".
-- The NPC wanders around while a player is near and answers a greeting.

wander = {}

-- Thinks per NPC, keyed by serial: a think comes every 500 ms (ultima.npcs.think_interval_ms),
-- so a step every 4th think is one step about every 2 seconds.
local thinks = {}

-- Called on every think of an NPC near a player. It must not call wait().
function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        npc.step(serial, dice.roll("1d8") - 1)
    end
end

-- Called when a player says something within 15 cells. It may call wait().
function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        wait(1)
        npc.say(serial, "Well met, traveller.")
    end
end
