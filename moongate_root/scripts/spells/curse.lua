-- ==============================================================================
-- Moongate - scripts/spells/curse.lua
--
-- What it is for:
--   The fourth circle spell Curse: it lowers the strength, the dexterity and
--   the intelligence of the target together, by 1 and a tenth of the caster's
--   Magery points, for 1.2 seconds a point of it; a curse of a stat that is as
--   strong or stronger stays. It is harmful: the caster is the aggressor of the
--   target (a criminal against an innocent, and an NPC fights back) and the
--   target's own cast may be ruined. Called by the spell service with the
--   caster, the target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   curse.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   curse.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

curse = {}

function curse.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function curse.cast(caster, target, info)
    magic.curse_all(caster, target.serial, info)
end
