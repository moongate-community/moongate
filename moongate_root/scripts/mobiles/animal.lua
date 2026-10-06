-- ==============================================================================
-- Moongate - scripts/mobiles/animal.lua
--
-- What it is for:
--   The animals that keep to themselves, as ModernUO's animal AI: they stroll
--   around their home, rest now and then with their idle sound and a fidget, and
--   never go for a player. One that is hit, or missed, fights back (the combat
--   service makes it answer) and chases who hit it until that one is lost, as a
--   monster does. A mobile template uses it with script_id = "animal": the
--   creatures whose UOX3 NPCAI is animal do. The behaviour is the shared
--   scripts/common/creature.lua with hunts = false.
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

animal = creature.new({ hunts = false })
