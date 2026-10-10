-- ==============================================================================
-- Moongate - scripts/spells/invisibility.lua
--
-- What it is for:
--   The sixth circle spell Invisibility: the target is hidden, as by the Hiding
--   skill, for 1.2 seconds a point of the caster's Magery, and may walk while it
--   lasts; running, or a swing, a spell or a harm of its own, shows it. Its
--   fight is stopped and it leaves war mode. A staff member, a creature that
--   is not a player's or a vendor (invulnerable) are refused, before anything
--   is spent: "This spell won't work on that!". The invisibility is saved with
--   the hidden state: after a restart a hidden player is shown by its first step.
--   Called by the spell service with the caster, the target
--   ({ kind = "mobile", serial }) and the data of the spell.
--
-- Functions:
--   invisibility.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   invisibility.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

invisibility = {}

local WONT_WORK = 501857   -- This spell won't work on that!
local PER_POINT = 1.2
local PUFF = 0x376A
local PUFF_SPEED = 10
local PUFF_DURATION = 15
-- Steps it may take while hidden: more than any time of the spell can walk; running shows it all the same.
local STEPS = 1000

function invisibility.check(caster, target, info)
    local who = target.serial

    if mobile.is_dead(who) or mobile.notoriety(who) == "invulnerable" or (world.is_staff(who) and not world.is_staff(caster)) then
        return WONT_WORK
    end
end

function invisibility.cast(caster, target, info)
    local who = target.serial
    local seconds = math.floor(magic.points(caster, "magery") * PER_POINT)
    local until_time = world.now() + seconds

    effect.on(who, PUFF, { speed = PUFF_SPEED, duration = PUFF_DURATION })

    if info.sound ~= 0 then
        mobile.play_sound(who, info.sound)
    end

    combat.stop(who)
    mobile.set_war_mode(who, false)
    mobile.set_hidden(who, true)
    mobile.set_stealth_steps(who, STEPS)
    mobile.set_prop(who, "magic.invisible_until", until_time)

    timer.after(seconds, function()
        local flags = mobile.flags(who)

        -- A second cast, or a hiding of its own, since: this timer is not the one that ends it.
        if flags and flags.hidden and (mobile.get_prop(who, "magic.invisible_until") or 0) <= world.now() then
            mobile.set_prop(who, "magic.invisible_until", nil)
            mobile.set_hidden(who, false)
        end
    end)
end
