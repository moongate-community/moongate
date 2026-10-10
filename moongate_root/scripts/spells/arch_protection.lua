-- ==============================================================================
-- Moongate - scripts/spells/arch_protection.lua
--
-- What it is for:
--   The fourth circle spell Arch Protection: the Protection of the second
--   circle on everyone within three tiles of the place picked who is alive and
--   has none already, with the sound of the place first. It is no harm to
--   anyone. Called by the spell service with the caster, the target
--   ({ kind = "location", map, x, y, z }) and the data of the spell.
--
-- Functions:
--   arch_protection.check(caster, target, info)   nothing refuses it
--   arch_protection.cast(caster, target, info)    the effect, once the cast
--                                      succeeded
-- ==============================================================================

local magic = require("common.magic")

arch_protection = {}

local RANGE = 3
local PLACE_SOUND = 0x299

function arch_protection.cast(caster, target, info)
    world.play_sound(target.map, target.x, target.y, target.z, PLACE_SOUND)

    for _, who in ipairs(magic.alive_in_range(target.map, target.x, target.y, RANGE)) do
        if magic.protect(caster, who) then
            magic.show(info, who)
        end
    end
end
