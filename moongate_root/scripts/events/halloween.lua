-- ==============================================================================
-- Moongate - scripts/events/halloween.lua
--
-- What it is for:
--   The hooks of the event halloween (data/schedule.toml). The game itself is
--   scripts/common/trick_or_treat.lua, which asks schedule.is_active, so the
--   hooks only tell everybody that the season began or ended.
--
-- Functions:
--   on_start(id, name)   the event began: everybody is told
--   on_end(id, name)     the event is over: everybody is told
-- ==============================================================================

halloween = {}

-- Messages of data/messages/<language>/moongate.toml.
local STARTED = 30236
local OVER = 30237

function halloween.on_start(id, name)
    world.broadcast(localization.get(STARTED))
end

function halloween.on_end(id, name)
    world.broadcast(localization.get(OVER))
end
