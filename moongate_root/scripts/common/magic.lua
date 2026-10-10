-- ==============================================================================
-- Moongate - scripts/common/magic.lua
--
-- What it is for:
--   The helpers the scripts of the spells (scripts/spells/<key>.lua) share, by
--   the classic rules: the skills of a cast, the chance a target resists, the
--   damage a spell does with its scalar, the offset and the time of a curse,
--   the effect and the sound of a spell, and what a spell refuses. Used with
--   local magic = require("common.magic").
--
-- Functions:
--   magic.random()                     the roll of a resist, 0 to 1; math.random
--   magic.points(who, skill)           a skill of a mobile in points, as
--                                      "magery" or "resisting_spells"; 0 for none
--   magic.refuse_dead(target)          a cliloc number for a target that is dead,
--                                      nil for one that is not: what check returns
--   magic.resist_percent(caster, target, circle)   the chance, 0 to 100, that
--                                      the target resists a spell of the circle
--   magic.resisted(caster, target, circle)   whether it does, and the target
--                                      may learn Resisting Spells by it
--   magic.damage_scalar(caster, target)   what Evaluating Intelligence, Magery
--                                      and a creature's body do to a damage
--   magic.damage(caster, target, info, base)   the damage a spell does, the
--                                      whole number of it, with resist and scalar
--   magic.curse_offset(caster)         how much a curse lowers a stat
--   magic.curse_seconds(caster)        how long a curse lasts
--   magic.show(info, target)           the sound and the effect of the spell
--                                      on the target, as data/spells.toml says
--   magic.curse(caster, target, info, stat)   the whole of a stat curse such as
--                                      Clumsy: the caster aggresses the target,
--                                      the target's own cast is disturbed, the
--                                      stat is lowered and the effect is shown
-- ==============================================================================

local magic = {}

magic.random = math.random

local WONT_WORK = 501857     -- This spell won't work on that!
local RESISTING = 501783     -- You feel yourself resisting magical energy.
local RESISTED_SHARE = 0.75
local HUMAN_ENEMY_SCALE = 2
local EFFECT_SPEED = 10
local SKILL_CAP = 120

function magic.points(who, skill_name)
    local skills = mobile.skills(who)

    return skills and skills[skill_name] or 0
end

function magic.refuse_dead(target)
    if mobile.is_dead(target) then
        return WONT_WORK
    end
end

function magic.resist_percent(caster, target, circle)
    local resist = magic.points(target, "resisting_spells")
    local by_skill = resist / 5
    local by_circle = resist - ((magic.points(caster, "magery") - 20) / 5 + circle * 5)

    return math.max(by_skill, by_circle) / 2
end

function magic.resisted(caster, target, circle)
    local chance = magic.resist_percent(caster, target, circle) / 100

    if chance <= 0 then
        return false
    end

    if chance >= 1 then
        return true
    end

    -- Resisting Spells may rise by the try, while it is under what the circle asks.
    local first = circle - 1
    local ceiling = circle * 10 + (1 + math.floor(first / 6)) * 25

    if magic.points(target, "resisting_spells") < ceiling then
        skill.check(target, "resisting_spells", 0, SKILL_CAP)
    end

    return chance >= magic.random()
end

function magic.damage_scalar(caster, target)
    local evaluating = magic.points(caster, "evaluating_intelligence")
    local resisting = magic.points(target, "resisting_spells")
    local scalar

    if evaluating > resisting then
        scalar = 1 + (evaluating - resisting) / 500
    else
        scalar = 1 + (evaluating - resisting) / 200
    end

    -- A damage bonus of the Magery skill: -25% at nothing, none at 100, +5% at 120.
    scalar = scalar + (magic.points(caster, "magery") - 100) / 400

    -- Double to the monsters and the animals, which are no player and have no human body.
    if not mobile.is_player(target) and mobile.body_type(target) ~= BodyType.Human then
        scalar = scalar * HUMAN_ENEMY_SCALE
    end

    return scalar
end

function magic.damage(caster, target, info, base)
    local damage = base

    if info.resistable and magic.resisted(caster, target, info.circle) then
        damage = damage * RESISTED_SHARE
        mobile.message_cliloc(target, RESISTING)
    end

    return math.max(1, math.floor(damage * magic.damage_scalar(caster, target)))
end

function magic.curse_offset(caster)
    return 1 + math.floor(magic.points(caster, "magery") * 0.1)
end

function magic.curse_seconds(caster)
    return math.floor(magic.points(caster, "magery") * 1.2)
end

function magic.show(info, target)
    if info.effect ~= 0 then
        effect.on(target, info.effect, { speed = EFFECT_SPEED, duration = info.effect_duration })
    end

    if info.sound ~= 0 then
        mobile.play_sound(target, info.sound)
    end
end

function magic.curse(caster, target, info, stat)
    combat.aggress(caster, target)
    -- A curse may ruin the spell its target is casting.
    spell.disturb(target)
    mobile.add_stat_curse(target, stat, magic.curse_offset(caster), magic.curse_seconds(caster))
    magic.show(info, target)
end

return magic
