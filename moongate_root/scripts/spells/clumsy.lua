-- ==============================================================================
-- Moongate - scripts/spells/clumsy.lua
--
-- What it is for:
--   The first circle spell Clumsy: it lowers the dexterity of the target by 1 and
--   a tenth of the caster's Magery points, for 1.2 seconds a point of it; a
--   curse of the same stat that is as strong or stronger stays. It is harmful:
--   the caster is the aggressor of the target (a criminal against an innocent,
--   and an NPC fights back) and the target's own cast may be ruined. Called by
--   the spell service with the caster, the target ({ kind = "mobile", serial })
--   and the data of the spell.
--
-- Functions:
--   clumsy.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent, nothing to go on
--   clumsy.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

clumsy = {}

function clumsy.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function clumsy.cast(caster, target, info)
    magic.curse(caster, target.serial, info, "dexterity")
end
