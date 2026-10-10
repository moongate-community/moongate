-- ==============================================================================
-- Moongate - scripts/spells/dispel_field.lua
--
-- What it is for:
--   The fifth circle spell Dispel Field: the field piece the caster picks, a wall of stone, a field of fire,
--   poison, paralysis or energy, or a gate that Gate Travel opened (and the gate at its other end), goes away
--   in a puff. A plain moongate is "too chaotic", and any other item cannot be dispelled; both are refused
--   before anything is spent. Called by the spell service with the caster, the target ({ kind = "item", serial
--   }) and the data of the spell.
--
-- Functions:
--   dispel_field.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   dispel_field.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

dispel_field = {}

local CANNOT_DISPEL = 1005049   -- That cannot be dispelled.
local TOO_CHAOTIC = 1005047     -- That magic is too chaotic.
local PUFF = 0x376A
local PUFF_SPEED = 9
local PUFF_DURATION = 20
local SOUND = 0x201

local DISPELLABLE = {
    magic_wall_of_stone = true,
    magic_fire_field_ew = true, magic_fire_field_ns = true,
    magic_poison_field_ew = true, magic_poison_field_ns = true,
    magic_paralyze_field_ew = true, magic_paralyze_field_ns = true,
    magic_energy_field_ew = true, magic_energy_field_ns = true,
    magic_gate = true,
}

function dispel_field.check(caster, target, info)
    local template = item.template(target.serial)

    if template == "moongate" then
        return TOO_CHAOTIC
    end

    if not DISPELLABLE[template] then
        return CANNOT_DISPEL
    end
end

function dispel_field.cast(caster, target, info)
    local serial = target.serial
    local at = item.location(serial)

    if not at then
        return
    end

    effect.at(at.map, at.x, at.y, at.z, PUFF, { speed = PUFF_SPEED, duration = PUFF_DURATION })
    world.play_sound(at.map, at.x, at.y, at.z, info.sound ~= 0 and info.sound or SOUND)

    -- A gate that Gate Travel opened takes the one at its other end with it.
    local partner = item.get_prop(serial, "gate.partner")

    item.delete(serial)

    if partner then
        item.delete(partner)
    end
end
