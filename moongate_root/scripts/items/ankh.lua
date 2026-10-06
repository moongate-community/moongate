-- ==============================================================================
-- Moongate - scripts/items/ankh.lua
--
-- What it is for:
--   The item script of the ankhs (item template decoration_ankh), as ModernUO's
--   AnkhWest and AnkhNorth: a ghost that double clicks one, from within two
--   cells, is asked in a gump whether it wants to come back to life. The living
--   have nothing to do with it.
--
-- Functions:
--   on_ghost_use(serial, user)  a dead player double clicked the ankh
-- ==============================================================================

ankh = {}

-- How far from the ankh the ghost may stand, in cells.
local use_range = 2

-- Client text: "That is too far away."
local too_far_cliloc = 500446

function ankh.on_ghost_use(serial, user)
    if not item.in_range(serial, user, use_range) then
        mobile.message_cliloc(user, too_far_cliloc)

        return true
    end

    gump.open(user, "resurrect", { ankh = serial })

    return true
end
