-- ==============================================================================
-- Moongate - scripts/spells/weaken.lua
--
-- What it is for:
--   The first circle spell Weaken: it lowers the strength of the target by 1 and
--   a tenth of the caster's Magery points, for 1.2 seconds a point of it; a
--   curse of the same stat that is as strong or stronger stays. It is harmful:
--   the caster is the aggressor of the target (a criminal against an innocent,
--   and an NPC fights back) and the target's own cast may be ruined. Called by
--   the spell service with the caster, the target ({ kind = "mobile", serial })
--   and the data of the spell.
--
-- Functions:
--   weaken.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent, nothing to go on
--   weaken.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

weaken = {}

function weaken.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function weaken.cast(caster, target, info)
    magic.curse(caster, target.serial, info, "strength")
end
