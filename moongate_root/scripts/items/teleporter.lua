-- ==============================================================================
-- Moongate - scripts/items/teleporter.lua
--
-- What it is for:
--   The item script of the teleporters (item template decoration_teleporter):
--   whoever walks onto one stands on its destination at once, as ModernUO's
--   Teleporter. Players never see the item; staff does.
--
-- Props it reads:
--   teleport.x, teleport.y, teleport.z   the destination; a teleporter without
--                                        all three does nothing
--   teleport.map   the destination map (a MapType number) when it is another
--                  one; without it the player stays on its map
--   active         false turns the teleporter off
--   deny_mounted   true makes it refuse whoever rides a mount: the rider stays
--                  where it is and is told to dismount first
--   creatures      true lets NPCs through too; without it only players travel
--   source_effect  true shows a puff of smoke where the mobile left
--   dest_effect    true shows a puff of smoke where the mobile arrived
--   sound_id       a sound played at the destination after the teleport
--
-- Functions:
--   on_move_over(serial, who)       a player stepped onto the teleporter
--   on_npc_move_over(serial, who)   an NPC stepped onto it
-- ==============================================================================

teleporter = {}

local teleport = require("common.teleport")

-- You must dismount before proceeding.
local MUST_DISMOUNT = 1077252

local function travel(serial, who)
    if item.get_prop(serial, "active") == false then
        return
    end

    if teleport.is_on(item.get_prop(serial, "deny_mounted")) and mobile.is_mounted(who) then
        mobile.message_cliloc(who, MUST_DISMOUNT)

        return
    end

    teleport.send(serial, who)
end

-- Called when a player steps onto the teleporter.
function teleporter.on_move_over(serial, who)
    travel(serial, who)
end

-- Called when an NPC steps onto the teleporter: as ModernUO, only one made for creatures takes it.
function teleporter.on_npc_move_over(serial, who)
    if teleport.is_on(item.get_prop(serial, "creatures")) then
        travel(serial, who)
    end
end
