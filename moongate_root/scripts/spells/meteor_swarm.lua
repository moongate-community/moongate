-- ==============================================================================
-- Moongate - scripts/spells/meteor_swarm.lua
--
-- What it is for:
--   The seventh circle spell Meteor Swarm: balls of fire fly from the caster to
--   everyone within two tiles of the place picked and, half a second later, each
--   takes 27 to 48 damage shared by the number of them. A target that resists
--   takes half of its share; then it is scaled by the caster's Evaluating
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
--   meteor_swarm.check(caster, target, info)   a cliloc number that refuses the
--                                      cast before anything is spent
--   meteor_swarm.cast(caster, target, info)    the effect, once the cast succeeded
--   meteor_swarm.random(low, high)     the roll of the damage, math.random
-- ==============================================================================

local magic = require("common.magic")

meteor_swarm = {}

meteor_swarm.random = math.random

local WONT_WORK = 501857   -- This spell won't work on that!
local RANGE = 2
local LEAST = 27
local MOST = 48
local RESISTED_SHARE = 0.5
local DAMAGE_DELAY = 0.5
local BALL = 0x36D4
local BALL_SPEED = 7

function meteor_swarm.check(caster, target, info)
    local refused = magic.refuse_in_town(target.map, target.x, target.y, target.z)

    if refused then
        return refused
    end

    if #magic.indirect_targets(caster, target.map, target.x, target.y, RANGE) == 0 then
        return WONT_WORK
    end
end

function meteor_swarm.cast(caster, target, info)
    local victims = magic.indirect_targets(caster, target.map, target.x, target.y, RANGE)

    if #victims == 0 then
        return
    end

    local damage = meteor_swarm.random(LEAST, MOST) / #victims

    if info.sound ~= 0 then
        world.play_sound(target.map, target.x, target.y, target.z, info.sound)
    end

    for _, who in ipairs(victims) do
        if combat.aggress(caster, who) then
            effect.moving(caster, who, info.projectile ~= 0 and info.projectile or BALL, {
                speed = info.projectile_speed ~= 0 and info.projectile_speed or BALL_SPEED,
            })
            magic.harm_after(caster, who, magic.damage(caster, who, info, damage, RESISTED_SHARE), DAMAGE_DELAY)
        end
    end
end
