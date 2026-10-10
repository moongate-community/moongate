-- ==============================================================================
-- Moongate - scripts/spells/chain_lightning.lua
--
-- What it is for:
--   The seventh circle spell Chain Lightning: a bolt strikes everyone within two
--   tiles of the place picked, half a second later, for 27 to 48 damage shared by
--   the number of them when there are more than two. A target that resists takes
--   half of its share; then it is scaled by the caster's Evaluating
--   Intelligence against the target's Resisting Spells, by the Magery of the
--   caster, and doubled against a monster or an animal. It does not strike the
--   caster, the dead, the invulnerable, the caster's own creatures nor a player
--   that looks innocent (unless the caster is a murderer); the caster is the
--   aggressor of each. It is refused, before anything is spent, at a guarded
--   town and when there is no one to strike. Called by the spell service with the
--   caster, the target ({ kind = "location", map, x, y, z }) and the data of the
--   spell.
--
-- Functions:
--   chain_lightning.check(caster, target, info)   a cliloc number that refuses
--                                      the cast before anything is spent
--   chain_lightning.cast(caster, target, info)    the effect, once the cast
--                                      succeeded
--   chain_lightning.random(low, high)  the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

chain_lightning = {}

chain_lightning.random = math.random

local WONT_WORK = 501857   -- This spell won't work on that!
local RANGE = 2
local LEAST = 27
local MOST = 48
local SHARED_ABOVE = 2
local RESISTED_SHARE = 0.5
local DAMAGE_DELAY = 0.5

function chain_lightning.check(caster, target, info)
    local refused = magic.refuse_in_town(target.map, target.x, target.y, target.z)

    if refused then
        return refused
    end

    if #magic.indirect_targets(caster, target.map, target.x, target.y, RANGE) == 0 then
        return WONT_WORK
    end
end

function chain_lightning.cast(caster, target, info)
    local victims = magic.indirect_targets(caster, target.map, target.x, target.y, RANGE)

    if #victims == 0 then
        return
    end

    local damage = chain_lightning.random(LEAST, MOST)

    if #victims > SHARED_ABOVE then
        damage = damage / #victims
    end

    for _, who in ipairs(victims) do
        if combat.aggress(caster, who) then
            effect.lightning(who)
            magic.harm_after(caster, who, magic.damage(caster, who, info, damage, RESISTED_SHARE), DAMAGE_DELAY)
        end
    end

    if info.sound ~= 0 then
        world.play_sound(target.map, target.x, target.y, target.z, info.sound)
    end
end
