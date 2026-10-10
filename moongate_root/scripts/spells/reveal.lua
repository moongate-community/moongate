-- ==============================================================================
-- Moongate - scripts/spells/reveal.lua
--
-- What it is for:
--   The sixth circle spell Reveal: everyone hidden within 1 tile and a twentieth
--   of the caster's Magery points (6 at 100) of the place picked is shown, with
--   a puff and a sound, the staff that is hidden excepted. It is refused, before
--   anything is spent, when no one is hidden there. Called by the spell service
--   with the caster, the target ({ kind = "location", map, x, y, z }) and the
--   data of the spell.
--
-- Functions:
--   reveal.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   reveal.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

reveal = {}

local WONT_WORK = 501857   -- This spell won't work on that!
local PER_POINT = 1 / 20
local EFFECT = 0x375A
local EFFECT_SPEED = 9
local EFFECT_DURATION = 20

-- Who is hidden within the range of the place, the caster and the hidden staff aside.
local function hidden_near(caster, target)
    local range = 1 + math.floor(magic.points(caster, "magery") * PER_POINT)
    local found = {}

    for _, who in ipairs(world.mobiles_in_range(target.map, target.x, target.y, range)) do
        local flags = mobile.flags(who)

        if who ~= caster and flags and flags.hidden and not mobile.is_dead(who) and not world.is_staff(who) then
            found[#found + 1] = who
        end
    end

    return found
end

function reveal.check(caster, target, info)
    if #hidden_near(caster, target) == 0 then
        return WONT_WORK
    end
end

function reveal.cast(caster, target, info)
    for _, who in ipairs(hidden_near(caster, target)) do
        mobile.set_hidden(who, false)
        effect.on(who, EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })
        mobile.play_sound(who, info.sound ~= 0 and info.sound or 0x1FD)
    end
end
