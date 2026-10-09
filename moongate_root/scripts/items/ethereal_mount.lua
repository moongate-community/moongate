-- ==============================================================================
-- Moongate - scripts/items/ethereal_mount.lua
--
-- What it is for:
--   The item script of the ethereal statuettes (templates/items/misc/
--   ethereal-statues.toml): a player double clicks one in its backpack and rides
--   the ethereal mount it stands for. The statuette is gone while the player
--   rides and comes back in its backpack when it gets off, or on the ground where
--   it stands when the backpack is full. A template uses it with
--   script_id = "ethereal_mount" and the tag mount_item, the template of the
--   mount item that is worn.
--
-- Functions:
--   on_use(serial, user)   the statuette was double clicked
-- ==============================================================================

ethereal_mount = {}

function ethereal_mount.on_use(serial, user)
    -- The mount module says why it refuses, in the client's own words.
    mount.ride_ethereal(user, serial)

    return true
end
