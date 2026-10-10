-- ==============================================================================
-- Moongate - scripts/spells/mass_curse.lua
--
-- What it is for:
--   The sixth circle spell Mass Curse: the Curse of the second circle on everyone
--   within two tiles of the place picked: the three stats lowered for 1.2 seconds
--   a point of the caster's Magery, and the cast of each ruined. It does not
--   touch the caster, the dead, the invulnerable, the caster's own creatures nor
--   a player that looks innocent (unless the caster is a murderer); the caster is
--   the aggressor of each. It is refused, before anything is spent, at a guarded
--   town and when no one is there to curse. Called by the spell service with the
--   caster, the target ({ kind = "location", map, x, y, z }) and the data of the
--   spell.
--
-- Functions:
--   mass_curse.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   mass_curse.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

mass_curse = {}

local WONT_WORK = 501857   -- This spell won't work on that!
local RANGE = 2

function mass_curse.check(caster, target, info)
    local refused = magic.refuse_in_town(target.map, target.x, target.y, target.z)

    if refused then
        return refused
    end

    if #magic.indirect_targets(caster, target.map, target.x, target.y, RANGE) == 0 then
        return WONT_WORK
    end
end

function mass_curse.cast(caster, target, info)
    for _, who in ipairs(magic.indirect_targets(caster, target.map, target.x, target.y, RANGE)) do
        magic.curse_all(caster, who, info)
    end
end
