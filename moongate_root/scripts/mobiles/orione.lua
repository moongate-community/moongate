-- ==============================================================================
-- Moongate - scripts/mobiles/orione.lua
--
-- What it is for:
--   Orione, a cat of Moongate v2: a lazy walker who is always hungry. The mobile
--   template "orione" (templates/mobiles/moongate_cats.toml) uses it.
--
-- Functions:
--   on_think(serial)                 every think of the cat near a player
--                                    (500 ms): a step about every 5 s, a line
--                                    every 2 s, a meow every 3 s
-- ==============================================================================

orione = {}

local MESSAGES = {
    "Meow meow! Ho fame!",
    "Meow, Voglio i chicchini!",
}

local SOUNDS = { 0x69, 0x6A }

-- Thinks per cat, keyed by serial: a think comes every 500 ms.
local thinks = {}

function orione.on_think(serial)
    local count = (thinks[serial] or 0) + 1
    thinks[serial] = count

    if count % 10 == 0 then
        npc.step(serial, dice.roll("1d8") - 1)
    end

    if count % 4 == 0 then
        npc.say(serial, MESSAGES[dice.roll("1d2")])
    end

    if count % 6 == 0 then
        npc.play_sound(serial, SOUNDS[dice.roll("1d2")])
    end
end
