-- ==============================================================================
-- Moongate - scripts/spells/reactive_armor.lua
--
-- What it is for:
--   The first circle spell Reactive Armor: for 25 seconds and half a second a
--   point of the caster's Magery, whole, a part of every melee blow that lands
--   on the target at arm's length goes back to whoever struck it: 10 per cent
--   and a quarter of a per cent a point of the Magery of the target when it is
--   hit (the combat service reads the prop magic.reactive_until and the skill).
--   An arrow does not go back, nor does a blow reach a guard. A target that
--   has it already is told so and nothing is spent. Called by the spell
--   service with the caster, the target ({ kind = "mobile", serial }) and the
--   data of the spell.
--
-- Functions:
--   reactive_armor.check(caster, target, info)   a cliloc number that refuses
--                                      the cast before anything is spent
--   reactive_armor.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

reactive_armor = {}

local IN_EFFECT = 1005559    -- This spell is already in effect.
local BASE_SECONDS = 25

function reactive_armor.check(caster, target, info)
    local who = target.serial
    local dead = magic.refuse_dead(who)

    if dead then
        return dead
    end

    if (mobile.get_prop(who, "magic.reactive_until") or 0) > world.now() then
        return IN_EFFECT
    end
end

function reactive_armor.cast(caster, target, info)
    local who = target.serial
    local magery = magic.points(caster, "magery")

    mobile.set_prop(who, "magic.reactive_until", world.now() + math.floor(BASE_SECONDS + magery / 2))
    magic.show(info, who)
end
