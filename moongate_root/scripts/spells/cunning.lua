-- ==============================================================================
-- Moongate - scripts/spells/cunning.lua
--
-- What it is for:
--   The second circle spell Cunning: it raises the intelligence of the target.
--   The offset is 1 and a tenth of the caster's Magery points, whole, and it
--   lasts 1.2 seconds a point of them; a buff of the stat that is as strong or
--   stronger stays, and a weaker one is replaced.
--   Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   cunning.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   cunning.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

cunning = {}

function cunning.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function cunning.cast(caster, target, info)
    magic.buff(caster, target.serial, info, { "intelligence" })
end
