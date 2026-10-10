-- ==============================================================================
-- Moongate - scripts/spells/resurrection.lua
--
-- What it is for:
--   The eighth circle spell Resurrection: the ghost of a player the caster picks,
--   within one tile of it, is asked to come back to life, with the sparkles and
--   the sound of the spell; the question is the gump of the ankhs and the
--   healers (templates/gumps/resurrect.xml), and what a resurrection costs there
--   it costs here (scripts/gumps/resurrect.lua). The caster itself, one that is
--   alive, a creature that is no player, a ghost more than a tile away and a
--   place where nothing can stand are refused, before anything is spent, each
--   with its own message. Called by the spell service with the caster, the
--   target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   resurrection.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   resurrection.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

resurrection = {}

local NOT_YOURSELF = 501039     -- Thou can not resurrect thyself.
local NOT_A_BEING = 501043      -- Target is not a being.
local NOT_DEAD = 501041         -- Target is not dead.
local TOO_FAR = 501042          -- Target is not close enough.
local CANNOT_THERE = 502391     -- Thou can not be resurrected there!
local RANGE = 1
local EFFECT = 0x376A
local EFFECT_SPEED = 10
local EFFECT_DURATION = 16

function resurrection.check(caster, target, info)
    local who = target.serial

    if who == caster then
        return NOT_YOURSELF
    end

    if not mobile.is_player(who) then
        return NOT_A_BEING
    end

    if not mobile.is_dead(who) then
        return NOT_DEAD
    end

    local here = mobile.location(caster)
    local there = mobile.location(who)

    if not here or not there or there.map ~= here.map or magic.distance(here, there) > RANGE then
        return TOO_FAR
    end

    if not world.can_fit(there.map, there.x, there.y, there.z, who, false) then
        mobile.message_cliloc(who, CANNOT_THERE)

        return TOO_FAR
    end
end

function resurrection.cast(caster, target, info)
    local who = target.serial

    mobile.play_sound(who, info.sound ~= 0 and info.sound or 0x214)
    effect.on(who, EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })
    gump.open(who, "resurrect", { caster = caster })
end
