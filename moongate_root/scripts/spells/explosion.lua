-- ==============================================================================
-- Moongate - scripts/spells/explosion.lua
--
-- What it is for:
--   The sixth circle spell Explosion: two and a half seconds after the cast the target, if it is alive still,
--   is blown up for 23 to 44 damage. A target that resists takes three quarters of it; then it is scaled by the
--   caster's Evaluating Intelligence against the target's Resisting Spells, by the Magery of the caster, and
--   doubled against a monster or an animal. The caster is the aggressor of the target from the cast. Called by
--   the spell service with the caster, the target ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   explosion.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   explosion.cast(caster, target, info)    the effect, once the cast succeeded
--   explosion.random(low, high)        the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

explosion = {}

explosion.random = math.random

local LEAST = 23
local MOST = 44
local DELAY = 2.5
local BLAST = 0x36BD
local BLAST_SPEED = 20
local BLAST_DURATION = 10
local BLAST_SOUND = 0x307

function explosion.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function explosion.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    timer.after(DELAY, function()
        -- Gone, or dead, in the meantime: nothing blows up.
        if mobile.location(who) == nil or mobile.is_dead(who) then
            return
        end

        local damage = magic.damage(caster, who, info, explosion.random(LEAST, MOST))

        effect.on(who, BLAST, { speed = BLAST_SPEED, duration = BLAST_DURATION })
        mobile.play_sound(who, BLAST_SOUND)
        magic.harm(caster, who, damage)
    end)
end
