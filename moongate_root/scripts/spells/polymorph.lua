-- ==============================================================================
-- Moongate - scripts/spells/polymorph.lua
--
-- What it is for:
--   The seventh circle spell Polymorph: the caster takes the body of an animal, a
--   monster or a man for 1.2 seconds a point of its Magery (a skin hue of a
--   human when the body is a man's, its own name still), by mobile.disguise; when
--   the time is up, or the caster dies, its own come back. A rider that takes a
--   body that is not a man's is dismounted.
--
--   The form is picked from a list. The first cast has no form: it opens the
--   list (templates/gumps/polymorph_forms.xml, scripts/gumps/polymorph_forms.lua) and spends
--   nothing; the pick casts the spell again with the body kept in the props
--   magic.polymorph_body and magic.polymorph_until, and that cast costs as any.
--
--   A caster that is disguised already, by Polymorph or by Incognito, is refused,
--   before anything is spent. Called by the spell service with the caster, the
--   target (none) and the data of the spell.
--
-- Functions:
--   polymorph.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent; false
--                                      when it opened the list of forms
--   polymorph.cast(caster, target, info)    the effect, once the cast succeeded
--   polymorph.random(low, high)        the roll of the skin hue, math.random
-- ==============================================================================

local magic = require("common.magic")

polymorph = {}

polymorph.random = math.random

local IN_EFFECT = 1005559         -- This spell is already in effect.
local WHILE_DISGUISED = 502167    -- You cannot polymorph while disguised.
local PER_POINT = 1.2
local FIRST_HUE, LAST_HUE = 0x3EA, 0x422   -- the skin hues of a human, data/races.toml
local HUMAN_BODIES = { [400] = true, [401] = true }   -- 0x190 and 0x191
local EFFECT = 0x375A
local EFFECT_SPEED = 10
local EFFECT_DURATION = 15
local KIND = "magic.disguise"

-- The body picked and still waiting for its cast; nil for none or one that is stale.
local function picked(caster)
    local body = mobile.get_prop(caster, "magic.polymorph_body")
    local until_time = mobile.get_prop(caster, "magic.polymorph_until") or 0

    if body and until_time >= world.now() then
        return body
    end

    return nil
end

function polymorph.check(caster, target, info)
    if mobile.is_disguised(caster) then
        return mobile.get_prop(caster, KIND) == "polymorph" and IN_EFFECT or WHILE_DISGUISED
    end

    if not picked(caster) then
        -- The list asks for the form, and the cast waits for it.
        mobile.set_prop(caster, "magic.polymorph_scroll", info.scroll == true)
        gump.open(caster, "polymorph_forms", {})

        return false
    end
end

function polymorph.cast(caster, target, info)
    local body = picked(caster)

    mobile.set_prop(caster, "magic.polymorph_body", nil)
    mobile.set_prop(caster, "magic.polymorph_until", nil)

    if not body then
        return
    end

    local seconds = math.floor(magic.points(caster, "magery") * PER_POINT)
    local hue = HUMAN_BODIES[body] and polymorph.random(FIRST_HUE, LAST_HUE) or 0

    if not mobile.disguise(caster, { body = body, hue = hue }, seconds) then
        return
    end

    mobile.set_prop(caster, KIND, "polymorph")
    effect.on(caster, EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })

    if info.sound ~= 0 then
        mobile.play_sound(caster, info.sound)
    end
end
