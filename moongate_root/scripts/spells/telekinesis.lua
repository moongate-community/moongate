-- ==============================================================================
-- Moongate - scripts/spells/telekinesis.lua
--
-- What it is for:
--   The third circle spell Telekinesis: the caster uses an item from afar, as a
--   double click would from beside it: a container is opened for it, and an item
--   whose script answers a double click does. An item that cannot be used so is
--   refused before anything is spent ("this spell won't work on that"). Called
--   by the spell service with the caster, the target
--   ({ kind = "item", serial }) and the data of the spell.
--
-- Functions:
--   telekinesis.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   telekinesis.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

telekinesis = {}

local WONT_WORK = 501857     -- This spell won't work on that!
local EFFECT_SPEED = 9

function telekinesis.check(caster, target, info)
    if not spell.can_use_from_afar(caster, target.serial) then
        return WONT_WORK
    end
end

function telekinesis.cast(caster, target, info)
    local at = item.location(target.serial)

    if at and info.effect ~= 0 then
        effect.at(at.map, at.x, at.y, at.z, info.effect, { speed = EFFECT_SPEED, duration = info.effect_duration })
    end

    if info.sound ~= 0 then
        item.play_sound(target.serial, info.sound)
    end

    spell.use_from_afar(caster, target.serial)
end
