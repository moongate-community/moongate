-- ==============================================================================
-- Moongate - scripts/spells/earthquake.lua
--
-- What it is for:
--   The eighth circle spell Earthquake: the ground shakes around the caster, and everyone within one tile and a
--   fifteenth of its Magery points (7 at 100) loses six tenths of its hit points, at least 10 for a creature
--   that is no player and at most 75, at once and with no resisting. It does not hurt the caster, the dead, the
--   invulnerable, the caster's own creatures nor a player that looks innocent (unless the caster is a
--   murderer); the caster is the aggressor of each. It is refused, before anything is spent, at a guarded town
--   and when there is no one around to hurt. Called by the spell service with the caster, the target (none) and
--   the data of the spell.
--
-- Functions:
--   earthquake.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   earthquake.cast(caster, target, info)    the effect, once the cast succeeded
-- ==============================================================================

local magic = require("common.magic")

earthquake = {}

local WONT_WORK = 501857   -- This spell won't work on that!
local PER_POINT = 1 / 15
local SHARE = 6 / 10
local LEAST_FOR_CREATURES = 10
local MOST = 75

local function range_of(caster)
    return 1 + math.floor(magic.points(caster, "magery") * PER_POINT)
end

function earthquake.check(caster, target, info)
    local here = mobile.location(caster)

    if not here then
        return false
    end

    local refused = magic.refuse_in_town(here.map, here.x, here.y, here.z)

    if refused then
        return refused
    end

    if #magic.indirect_targets(caster, here.map, here.x, here.y, range_of(caster)) == 0 then
        return WONT_WORK
    end
end

function earthquake.cast(caster, target, info)
    local here = mobile.location(caster)

    if not here then
        return
    end

    world.play_sound(here.map, here.x, here.y, here.z, info.sound ~= 0 and info.sound or 0x220)

    for _, who in ipairs(magic.indirect_targets(caster, here.map, here.x, here.y, range_of(caster))) do
        local stats = mobile.stats(who)

        if stats and combat.aggress(caster, who) then
            local damage = math.floor(stats.hits * SHARE)

            if not mobile.is_player(who) and damage < LEAST_FOR_CREATURES then
                damage = LEAST_FOR_CREATURES
            elseif damage > MOST then
                damage = MOST
            end

            magic.harm(caster, who, damage)
        end
    end
end
