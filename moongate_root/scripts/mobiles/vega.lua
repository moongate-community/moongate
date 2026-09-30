-- ==============================================================================
-- Moongate - scripts/mobiles/vega.lua
--
-- What it is for:
--   Vega, a cat of Moongate v2: always on the move and looking for the terrace.
--   The mobile template "vega" (templates/mobiles/moongate_cats.toml) uses it.
--
-- Functions:
--   on_think(serial)                 every think of the cat near a player
--                                    (500 ms): a step every second, a line
--                                    every 2 s, a meow every 3 s
--   on_speech(serial, speaker, text) a player says hello within 15 cells: Vega
--                                    counts the hellos in the prop
--                                    vega.greeted, kept across restarts
-- ==============================================================================

vega = {}

local MESSAGES = {
    "Miaaow! I want to go out on the terrace!",
    "Meow... I'm hiding in the wardrobe!",
}

-- Kinds of the cat template's [mobile.sounds], so the cat makes its own sounds.
local SOUNDS = { "idle", "start_attack" }

-- Thinks per cat, keyed by serial: a think comes every 500 ms.
local thinks = {}

function vega.on_think(serial)
    local count = (thinks[serial] or 0) + 1
    thinks[serial] = count

    if count % 2 == 0 then
        npc.step(serial, dice.roll("1d8") - 1)
    end

    if count % 4 == 0 then
        npc.say(serial, MESSAGES[dice.roll("1d2")])
    end

    if count % 6 == 0 then
        npc.play_sound(serial, SOUNDS[dice.roll("1d2")])
    end
end

-- Called when a player says something within 15 cells. The count is a prop of the cat, saved with it by the world save,
-- so Vega remembers it after a restart; a local table would start again from zero.
function vega.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        local times = (npc.get_prop(serial, "vega.greeted") or 0) + 1
        npc.set_prop(serial, "vega.greeted", times)
        npc.say(serial, "Meow! That's " .. times .. " hellos.")
    end
end
