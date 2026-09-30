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
-- ==============================================================================

vega = {}

local MESSAGES = {
    "Miaaow! I want to go out on the terrace!",
    "Meow... I'm hiding in the wardrobe!",
}

local SOUNDS = { 0x69, 0x6A }

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
