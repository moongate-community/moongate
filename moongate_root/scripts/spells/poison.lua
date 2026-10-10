-- ==============================================================================
-- Moongate - scripts/spells/poison.lua
--
-- What it is for:
--   The third circle spell Poison: the target is poisoned unless it resists
--   (it is told it did). The level goes by the caster's Magery and Poisoning
--   points together, less ten for each tile beyond three the target stands at:
--   over 199.8 the deadly poison one time in ten and else the greater, over
--   170.2 the greater, over 130.2 the regular and else the lesser. The target's
--   own cast is disturbed and the caster is its aggressor. Called by the spell
--   service with the caster, the target ({ kind = "mobile", serial }) and the
--   data of the spell.
--
-- Functions:
--   poison.check(caster, target, info)   a cliloc number that refuses the cast
--                                      before anything is spent
--   poison.cast(caster, target, info)    the effect, once the cast succeeded
--   poison.level(total, roll)          the level of a poison from the points
--                                      and a roll of 0 to 1 for the deadly one
-- ==============================================================================

local magic = require("common.magic")

poison = {}

local RESISTING = 501783     -- You feel yourself resisting magical energy.
local FAR = 3                -- tiles at which the level starts to fall
local PER_TILE = 10          -- points lost for each tile beyond FAR
local DEADLY_ONE_IN = 10

function poison.level(total, roll)
    if total > 199.8 then
        return roll < 1 / DEADLY_ONE_IN and 3 or 2
    elseif total > 170.2 then
        return 2
    elseif total > 130.2 then
        return 1
    end

    return 0
end

function poison.check(caster, target, info)
    return magic.refuse_dead(target.serial)
end

function poison.cast(caster, target, info)
    local who = target.serial

    if not combat.aggress(caster, who) then
        return
    end

    -- A poison may ruin the spell its target is casting.
    spell.disturb(who)

    if magic.resisted(caster, who, info.circle) then
        mobile.message_cliloc(who, RESISTING)
    else
        local total = magic.points(caster, "magery") + magic.points(caster, "poisoning")
        local from = mobile.location(caster)
        local to = mobile.location(who)

        if from and to then
            local distance = magic.euclid(from, to)

            if distance >= FAR then
                total = total - (distance - FAR) * PER_TILE
            end
        end

        mobile.poison(who, poison.level(total, magic.random()))
    end

    magic.show(info, who)
end
