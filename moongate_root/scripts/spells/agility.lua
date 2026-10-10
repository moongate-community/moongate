-- ==============================================================================
-- Moongate - scripts/spells/agility.lua
--
-- What it is for:
--   The second circle spell Agility: it raises the dexterity of the target.
--   The offset is 1 and a tenth of the caster's Magery points, whole, and it
--   lasts 1.2 seconds a point of them; a buff of the stat that is as strong or
--   stronger stays, and a weaker one is replaced.
--   Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   agility.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   agility.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

agility = {}

function agility.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function agility.cast(caster, target, info)
    magic.buff(caster, target.serial, info, { "dexterity" })
end
