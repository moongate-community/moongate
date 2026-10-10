-- ==============================================================================
-- Moongate - scripts/spells/magic_reflection.lua
--
-- What it is for:
--   The fifth circle spell Magic Reflection, as the classic game had it: the caster is wrapped in a reflection,
--   the prop magic.reflect, which turns the first harmful spell that can be reflected and that is aimed at it
--   back on the one who cast it, and is used up by it (the cast service does that, in SpellCastService). It
--   does not turn a spell of a place, nor a spell the caster aims at itself; it has no time, and a death ends
--   it. A second cast while it is on is refused, before anything is spent. Called by the spell service with the
--   caster, the target (none) and the data of the spell.
--
-- Functions:
--   magic_reflection.check(caster, target, info)   a cliloc number that
--                                      refuses the cast before anything is spent
--   magic_reflection.cast(caster, target, info)    the effect, once the cast
--                                      succeeded
-- ==============================================================================

magic_reflection = {}

local IN_EFFECT = 1005559   -- This spell is already in effect.
local EFFECT_SPEED = 10
local EFFECT_DURATION = 15

function magic_reflection.check(caster, target, info)
    if mobile.get_prop(caster, "magic.reflect") then
        return IN_EFFECT
    end
end

function magic_reflection.cast(caster, target, info)
    mobile.set_prop(caster, "magic.reflect", true)
    effect.on(caster, info.effect ~= 0 and info.effect or 0x375A, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })

    if info.sound ~= 0 then
        mobile.play_sound(caster, info.sound)
    end
end
