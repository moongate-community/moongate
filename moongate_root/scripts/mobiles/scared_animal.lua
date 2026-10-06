-- ==============================================================================
-- Moongate - scripts/mobiles/scared_animal.lua
--
-- What it is for:
--   The animals that run, as ModernUO's flighty creatures: they stroll around
--   their home like any animal, but one that is hit, or missed, does not fight
--   back: it runs from who did, up to twelve cells and for ten seconds at most,
--   then goes back to strolling. A mobile template uses it with
--   script_id = "scared_animal": the creatures whose UOX3 NPCAI is scared
--   animal do. The behaviour is the shared scripts/common/creature.lua with
--   flees = true.
--
-- Functions:
--   on_think(serial)   every think of an NPC near a player
--                      (ultima.npcs.think_interval_ms, 500 ms); must not call
--                      wait()
--
-- What it keeps:
--   Its state is kept in memory by serial, not saved.
-- ==============================================================================

local creature = require("common.creature")

scared_animal = creature.new({ flees = true })
