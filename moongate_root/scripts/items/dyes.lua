-- ==============================================================================
-- Moongate - scripts/items/dyes.lua
--
-- What it is for:
--   The item script of the dyes, as in ModernUO: double clicking them gives a
--   cursor to pick a dye tub, then the client's hue picker with the tub in it;
--   the tub takes the hue picked, from 2 to 1001, and keeps it as its own hue.
--   The dyes are never used up. An item template uses it with
--   script_id = "dyes"; a dye tub is an item whose template has
--   script_id = "dye_tub".
--
--   The player may answer the picker much later, or never: when the answer
--   comes, the dyes and the tub must still be within reach, or nothing
--   changes. The other emulators take the answer as it comes.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the dyes; returns true
-- ==============================================================================

dyes = {}

local dye = require("common.dye")

-- The client's own texts.
local SELECT_TUB = 500856    -- Select the dye tub to use the dyes on.
local USE_ON_A_TUB = 500857  -- Use this on a dye tub.

local function within_reach(serial, tub, user)
    return item.script(tub) == "dye_tub" and dye.reach(serial, user) and dye.reach(tub, user)
end

-- Called when a player double clicks the dyes.
function dyes.on_use(serial, user)
    dye.tell(user, SELECT_TUB)

    target.pick(user, function(picked)
        if picked.kind ~= "object" then
            return
        end

        local tub = picked.serial

        if item.script(tub) ~= "dye_tub" then
            dye.tell(user, USE_ON_A_TUB)
            return
        end

        if not within_reach(serial, tub, user) then
            dye.tell(user, dye.TOO_FAR)
            return
        end

        hue_picker.open(user, item.item_id(tub), function(hue)
            -- Another picker took its place, or the player left.
            if not hue then
                return
            end

            if not within_reach(serial, tub, user) then
                dye.tell(user, dye.TOO_FAR)
                return
            end

            -- A tub held on the cursor takes no hue.
            if not item.set_hue(tub, hue) then
                dye.tell(user, dye.CANNOT_DYE)
            end
        end)
    end)

    return true
end
