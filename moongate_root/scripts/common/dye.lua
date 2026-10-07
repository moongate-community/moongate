-- ==============================================================================
-- Moongate - scripts/common/dye.lua
--
-- What it is for:
--   What the two item scripts of dyeing share: the dyes (items/dyes.lua) and
--   the dye tub (items/dye_tub.lua). A script takes it with
--   local dye = require("common.dye").
--
-- Functions:
--   dye.tell(user, number)    tells the player a text of the client by its
--                             cliloc number
--   dye.worn(serial)          whether a mobile wears the item
--   dye.reach(serial, user)   whether the player reaches the item: it carries
--                             it, at any depth of its backpack, or the item
--                             lies on the ground within 1 tile, as ModernUO
--                             asks of the tub and of what it dyes
-- ==============================================================================

local dye = {}

-- The client's own texts.
dye.TOO_FAR = 500446     -- That is too far away.
dye.CANNOT_DYE = 1042083 -- You can not dye that.

local RANGE = 1

function dye.tell(user, number)
    mobile.message_cliloc(user, number)
end

function dye.worn(serial)
    return item.owner(serial) ~= nil and item.container(serial) == nil
end

function dye.reach(serial, user)
    if item.owner(serial) == user then
        return not dye.worn(serial)
    end

    return item.in_range(serial, user, RANGE)
end

return dye
