-- ==============================================================================
-- Moongate - scripts/items/dye_tub.lua
--
-- What it is for:
--   The item script of the dye tub, as in ModernUO: double clicking it gives a
--   cursor to pick what to dye, which takes the hue of the tub with the sound
--   of dyeing. What can be dyed is an item whose template says
--   dyeable = true, as clothing does. It must not be worn, and the player must
--   reach it and the tub: carried, or on the ground within 1 tile. The tub is
--   never used up; the dyes (items/dyes.lua) give it its hue, and one never
--   dyed has hue 0, which takes the colour off. An item template uses it with
--   script_id = "dye_tub".
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the tub; returns true
-- ==============================================================================

dye_tub = {}

local dye = require("common.dye")

local SOUND = 0x23E

-- The client's own texts.
local SELECT_CLOTHING = 500859  -- Select the clothing to dye.
local WORN = 500861             -- Can't Dye clothing that is being worn.
local CANNOT_DYE = 1042083      -- You can not dye that.

-- Called when a player double clicks the tub.
function dye_tub.on_use(serial, user)
    dye.tell(user, SELECT_CLOTHING)

    target.pick(user, function(picked)
        if picked.kind ~= "object" then
            return
        end

        local what = picked.serial

        if not item.dyeable(what) then
            dye.tell(user, CANNOT_DYE)
            return
        end

        if dye.worn(what) and item.owner(what) == user then
            dye.tell(user, WORN)
            return
        end

        if not dye.reach(serial, user) or not dye.reach(what, user) then
            dye.tell(user, dye.TOO_FAR)
            return
        end

        if item.set_hue(what, item.hue(serial) or 0) then
            item.play_sound(serial, SOUND)
        end
    end)

    return true
end
