-- ==============================================================================
-- Moongate - scripts/items/potion.lua
--
-- What it is for:
--   An item script: double clicking the potion drinks one. An item template
--   uses it with script_id = "potion"; the file is named after its script_id and
--   defines the global table of the same name. It does not heal yet: there are
--   no hit points to restore.
--
-- Functions:
--   on_use(serial, user)             the player user double clicks the item,
--                                    carried or on the ground within 2 tiles;
--                                    return true to stop the default action;
--                                    may call wait()
-- ==============================================================================

potion = {}

-- Called when a player double clicks the potion.
function potion.on_use(serial, user)
    item.message(serial, user, "You drink the potion.")
    item.consume(serial)

    return true
end
