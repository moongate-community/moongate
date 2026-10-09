-- ==============================================================================
-- Moongate - scripts/events/halloween.lua
--
-- What it is for:
--   The hooks of the event halloween (data/schedule.toml). The game itself is
--   scripts/common/trick_or_treat.lua, which asks schedule.is_active, so the
--   hooks tell everybody that the season began or ended, and put the
--   decorations in the towns or take them away (common/holiday_decor.lua).
--
-- Functions:
--   on_start(id, name)   the event began: the towns are decorated, everybody is told
--   on_end(id, name)     the event is over: the decorations go, everybody is told
-- ==============================================================================

local holiday_decor = require("common.holiday_decor")

halloween = {}

-- The decorations of the towns, templates of templates/items/misc/holiday_decorations.toml.
local TOWN_DECORATIONS = {
    "hw_jack_o_lantern", "hw_pumpkin", "hw_skulls_on_pike", "hw_pumpkin_scarecrow", "hw_black_cat",
    "hw_ghoul_statue", "hw_leering_jack_o_lantern"
}

-- Messages of data/messages/<language>/moongate.toml.
local STARTED = 30236
local OVER = 30237

function halloween.on_start(id, name)
    holiday_decor.place(id, TOWN_DECORATIONS)
    world.broadcast(localization.get(STARTED))
end

function halloween.on_end(id, name)
    holiday_decor.remove(id)
    world.broadcast(localization.get(OVER))
end
