-- ==============================================================================
-- Moongate - scripts/spells/bless.lua
--
-- What it is for:
--   The third circle spell Bless: it raises the strength, the dexterity and the
--   intelligence of the target. The offset is 1 and a tenth of the caster's
--   Magery points, whole, and it lasts 1.2 seconds a point of them; a buff of
--   a stat that is as strong or stronger stays, and a weaker one is replaced.
--   Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   bless.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   bless.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

bless = {}

function bless.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function bless.cast(caster, target, info)
    magic.buff(caster, target.serial, info, { "strength", "dexterity", "intelligence" })
end
