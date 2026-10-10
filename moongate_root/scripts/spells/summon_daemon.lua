-- ==============================================================================
-- Moongate - scripts/spells/summon_daemon.lua
--
-- What it is for:
--   The eighth circle spell Summon Daemon: a daemon (daemon_summon) is called beside the caster and fights for
--   it, by the pet order guard, for as many seconds as the caster has points of Magery, or until it is
--   dispelled or killed, and costs the caster 70 points of karma. It counts for the control slots of its
--   template as a follower. It is refused, before anything is spent, when the caster has too many followers for
--   it and when no place beside the caster is free. Called by the spell service with the caster, the target
--   (none) and the data of the spell.
--
-- Functions:
--   summon_daemon.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   summon_daemon.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local summon = require("common.summon")
local magic = require("common.magic")

summon_daemon = {}

local BLOCKED = 501942   -- That location is blocked.
local TEMPLATE = "daemon_summon"
local KARMA_COST = 70

function summon_daemon.check(caster, target, info)
    local refused = summon.refuse_template(caster, TEMPLATE)

    if refused then
        return refused
    end

    if not summon.near(caster) then
        return BLOCKED
    end
end

function summon_daemon.cast(caster, target, info)
    local place = summon.near(caster)

    if not place then
        return
    end

    local seconds = math.max(magic.points(caster, "magery"), 1)

    if summon.create(caster, TEMPLATE, place, seconds, info.sound) then
        -- Calling a daemon costs the caster 70 points of karma.
        local stats = mobile.stats(caster)

        if stats then
            mobile.set_stats(caster, { karma = stats.karma - KARMA_COST })
        end
    end
end
