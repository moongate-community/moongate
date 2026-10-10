-- ==============================================================================
-- Moongate - scripts/spells/incognito.lua
--
-- What it is for:
--   The fifth circle spell Incognito: for 1.2 seconds a point of the caster's
--   Magery (at most 144) it has a random skin hue and a random name of its sex
--   (the lists male and female of data/names.toml), by mobile.disguise; when the
--   time is up, or the caster dies, its own come back. Its hair and beard are
--   not changed. A caster that is disguised already, by Incognito or by
--   Polymorph, is refused before anything is spent. Called by the spell service
--   with the caster, the target (none) and the data of the spell.
--
-- Functions:
--   incognito.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   incognito.cast(caster, target, info)    the effect, once the cast succeeded
--   incognito.random(low, high)        the roll of the hue, math.random
-- ==============================================================================

local magic = require("common.magic")

incognito = {}

incognito.random = math.random

local IN_EFFECT = 1005559         -- This spell is already in effect.
local WHILE_DISGUISED = 1061631   -- You can't do that while disguised.
local MOST_SECONDS = 144
local PER_POINT = 1.2
local FIRST_HUE, LAST_HUE = 0x3EA, 0x422   -- the skin hues of a human, data/races.toml
local HEAD_EFFECT = 0x373A
local EFFECT_SPEED = 10
local EFFECT_DURATION = 15
local KIND = "magic.disguise"

function incognito.check(caster, target, info)
    if mobile.is_disguised(caster) then
        return mobile.get_prop(caster, KIND) == "incognito" and IN_EFFECT or WHILE_DISGUISED
    end
end

function incognito.cast(caster, target, info)
    local seconds = math.min(magic.points(caster, "magery") * PER_POINT, MOST_SECONDS)
    local list = mobile.is_female(caster) and "female" or "male"

    if not mobile.disguise(caster, { name_list = list, hue = incognito.random(FIRST_HUE, LAST_HUE) }, seconds) then
        return
    end

    mobile.set_prop(caster, KIND, "incognito")
    effect.on(caster, HEAD_EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })

    if info.sound ~= 0 then
        mobile.play_sound(caster, info.sound)
    end
end
