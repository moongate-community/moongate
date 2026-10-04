-- ==============================================================================
-- Moongate - scripts/common/teleport.lua
--
-- What it is for:
--   What the item scripts that teleport share: the teleporters one walks onto
--   (items/teleporter.lua) and those that answer a word
--   (items/keyword_teleport.lua). A script takes it with
--   local teleport = require("common.teleport").
--
-- Props of the item it reads:
--   teleport.x, teleport.y, teleport.z   the destination; without all three
--                                        nothing happens
--   teleport.map   the destination map (a MapType number) when it is another
--                  one; without it the mobile stays on its map
--   source_effect  true shows a puff of smoke where the mobile left
--   dest_effect    true shows a puff of smoke where the mobile arrived
--   sound_id       a sound, a number above 0, played at the destination after
--                  the teleport
--
-- Functions:
--   teleport.is_on(value)      whether a flag is set: the decoration files
--                              carry the flags as text
--   teleport.send(serial, who) sends the mobile where the item's props say,
--                              with the smoke and the sound they ask for;
--                              true when it travelled
-- ==============================================================================

local teleport = {}

-- The decoration files carry the flags as text.
function teleport.is_on(value)
    return value == true or value == "true"
end

function teleport.send(serial, who)
    local x = item.get_prop(serial, "teleport.x")
    local y = item.get_prop(serial, "teleport.y")
    local z = item.get_prop(serial, "teleport.z")

    if not (x and y and z) then
        return false
    end

    -- A nil map keeps the mobile on its own.
    local map = item.get_prop(serial, "teleport.map")

    -- As ModernUO: the smoke where the mobile leaves is shown before the move, to those who watch it go.
    local from = mobile.location(who)

    if not from then
        return false
    end

    if teleport.is_on(item.get_prop(serial, "source_effect")) then
        effect.at(from.map, from.x, from.y, from.z, EffectGraphicType.Smoke)
    end

    if not mobile.teleport(who, x, y, z, map) then
        return false
    end

    if teleport.is_on(item.get_prop(serial, "dest_effect")) then
        effect.at(map or from.map, x, y, z, EffectGraphicType.Smoke)
    end

    local sound = tonumber(item.get_prop(serial, "sound_id"))

    if sound and sound > 0 then
        mobile.play_sound(who, sound)
    end

    return true
end

return teleport
