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
--   teleport.map   the destination map (a MapType number); a teleporter to
--                  another map does nothing yet
--   active         false turns the teleporter off
--   source_effect  true shows a puff of smoke where the mobile left
--   dest_effect    true shows a puff of smoke where the mobile arrived
--   sound_id       a sound played at the destination after the teleport
--
-- Functions:
--   on_move_over(serial, who)   a player stepped onto the teleporter
-- ==============================================================================

teleporter = {}

-- The decoration files carry the flags as text.
local function is_on(value)
    return value == true or value == "true"
end

-- Called when a player steps onto the teleporter.
function teleporter.on_move_over(serial, who)
    if item.get_prop(serial, "active") == false then
        return
    end

    local x = item.get_prop(serial, "teleport.x")
    local y = item.get_prop(serial, "teleport.y")
    local z = item.get_prop(serial, "teleport.z")

    if not (x and y and z) then
        return
    end

    local map = item.get_prop(serial, "teleport.map")
    local here = item.location(serial)

    if map and (not here or map ~= here.map) then
        return
    end

    -- As ModernUO: the smoke where the mobile leaves is shown before the move, to those who watch it go.
    local from = mobile.location(who)

    if not from then
        return
    end

    if is_on(item.get_prop(serial, "source_effect")) then
        effect.at(from.map, from.x, from.y, from.z, EffectGraphicType.Smoke)
    end

    if mobile.teleport(who, x, y, z) then
        if is_on(item.get_prop(serial, "dest_effect")) then
            effect.at(from.map, x, y, z, EffectGraphicType.Smoke)
        end

        local sound = item.get_prop(serial, "sound_id")

        if sound then
            mobile.play_sound(who, sound)
        end
    end
end
