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
--                                      the target's own cast is disturbed, its
--                                      paralysis ended, the stat is lowered and
--                                      the effect is shown;
--                                      nothing at all when the target cannot be
--                                      harmed (combat.aggress is false)
--   magic.curse_all(caster, target, info)   the same for the three stats at once,
--                                      as Curse does
--   magic.buff(caster, target, info, stats)   the whole of a stat buff such as
--                                      Agility: each stat of the list is raised
--                                      as a curse is lowered, a stronger buff
--                                      of the stat replacing a weaker one, and
--                                      the effect is shown
--   magic.protected(who)               whether the mobile has the armor of a
--                                      Protection on now
--   magic.protect(caster, who)         puts that armor on a mobile that has none:
--                                      a tenth of the caster's Magery points of
--                                      armor for 1.2 seconds a point; false when
--                                      it has it already
--   magic.refuse_unhealable(caster, target)   the cliloc a heal is refused with
--                                      for a dead, poisoned or whole target
--   magic.distance(a, b)               the distance in tiles of two places
--                                      ({ x, y }): the larger of the two
--                                      differences, which is how far a spell reaches
--   magic.euclid(a, b)                 the straight distance of two places, which
--                                      Poison weighs its level by
--   magic.alive_in_range(map, x, y, range)   the serials of the mobiles that are
--                                      not dead within range tiles of a place
--   magic.refuse_in_town(map, x, y, z)   the cliloc 500946 that refuses a spell
--                                      of the sixth circle and above cast, or
--                                      aimed, at a guarded town; nil for none
--   magic.valid_indirect(caster, who)  whether a spell that hits a place may
--                                      hit the mobile: not the dead, not a
--                                      staff member that is hidden, not the
--                                      caster's own creatures, and not a
--                                      player or an owned creature that looks
--                                      innocent, unless the caster is a
--                                      murderer. The caster itself is valid
--   magic.indirect_targets(caster, map, x, y, range)   the serials, the caster
--                                      and the invulnerable left out, that a
--                                      spell of a place may hit within range
--   magic.harm(caster, who, damage)    a blow of the caster's; the damage is
--                                      done, with no one to blame, when the
--                                      caster has left the game
--   magic.harm_after(caster, who, damage, seconds)   the same, a while later
--   magic.aggress(caster, who, info)   makes the caster the aggressor of who and
--                                      says whether the spell goes on; false for
--                                      one that cannot be harmed. A spell that
--                                      Magic Reflection turned back (info.reflected)
--                                      has the caster for who: it is the aggressor
--                                      of the wearer (info.reflector) it aimed at
--                                      and hurts itself, with no crime
--   magic.dispel_chance(caster, difficulty, focus)   the chance, 0 to 1 or
--                                      more, that a Dispel undoes a summoned
--                                      creature of that difficulty and focus
-- ==============================================================================

local magic = {}

magic.random = math.random

local WONT_WORK = 501857     -- This spell won't work on that!
local CANNOT_HEAL_SELF = 1005000    -- You can not heal yourself in your current state.
local CANNOT_HEAL_OTHER = 1010398   -- You can not heal that person in their current state.
local RESISTING = 501783     -- You feel yourself resisting magical energy.
local IN_TOWN = 500946       -- You cannot cast this in town!
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

function magic.resisted(caster, target, circle, percent)
    -- A spell may name its own chance, such as Mana Drain's nearly sure one.
    local chance = (percent or magic.resist_percent(caster, target, circle)) / 100

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

function magic.damage(caster, target, info, base, share)
    local damage = base

    -- The share that is kept when the target resists: three quarters, and a spell may keep less.
    if info.resistable and magic.resisted(caster, target, info.circle) then
        damage = damage * (share or RESISTED_SHARE)
        mobile.message_cliloc(target, RESISTING)
    end

    return math.max(1, math.floor(damage * magic.damage_scalar(caster, target)))
end

function magic.curse_offset(caster)
    return 1 + math.floor(magic.points(caster, "magery") * 0.1)
end

function magic.refuse_unhealable(caster, target)
    local dead = magic.refuse_dead(target)

    if dead then
        return dead
    end

    if mobile.poison_level(target) then
        return caster == target and CANNOT_HEAL_SELF or CANNOT_HEAL_OTHER
    end

    local stats = mobile.stats(target)

    if stats and stats.hits >= stats.hits_max then
        return WONT_WORK
    end
end

function magic.distance(a, b)
    return math.max(math.abs(a.x - b.x), math.abs(a.y - b.y))
end

function magic.euclid(a, b)
    local dx = a.x - b.x
    local dy = a.y - b.y

    return math.sqrt(dx * dx + dy * dy)
end

function magic.alive_in_range(map, x, y, range)
    local alive = {}

    for _, who in ipairs(world.mobiles_in_range(map, x, y, range)) do
        if not mobile.is_dead(who) then
            alive[#alive + 1] = who
        end
    end

    return alive
end

function magic.refuse_in_town(map, x, y, z)
    if world.is_guarded(map, x, y, z) then
        return IN_TOWN
    end
end

function magic.valid_indirect(caster, who)
    if who == caster then
        return true
    end

    if mobile.is_dead(who) then
        return false
    end

    local flags = mobile.flags(who)

    if flags and flags.hidden and world.is_staff(who) then
        return false
    end

    local owner = mobile.get_prop(who, "owner")

    if owner == caster then
        return false
    end

    -- A blue player, or a creature that somebody owns and that looks blue, is not hit by a spell of a place.
    if (mobile.is_player(who) or (owner ~= nil and owner ~= 0)) and mobile.notoriety(who) == "innocent" and
        not mobile.is_murderer(caster) then
        return false
    end

    return true
end

function magic.indirect_targets(caster, map, x, y, range)
    local found = {}

    for _, who in ipairs(world.mobiles_in_range(map, x, y, range)) do
        if who ~= caster and mobile.notoriety(who) ~= "invulnerable" and magic.valid_indirect(caster, who) then
            found[#found + 1] = who
        end
    end

    return found
end

function magic.aggress(caster, who, info)
    if info.reflected then
        combat.aggress(caster, info.reflector)

        return true
    end

    return combat.aggress(caster, who)
end

function magic.harm(caster, who, damage)
    -- A caster that left the game in the meantime is to blame for nothing, but the damage is done all the same.
    if not combat.harm(who, damage, caster) and not mobile.location(caster) then
        combat.harm(who, damage)
    end
end

function magic.harm_after(caster, who, damage, seconds)
    timer.after(seconds, function()
        magic.harm(caster, who, damage)
    end)
end

function magic.dispel_chance(caster, difficulty, focus)
    return (50 + 100 * (magic.points(caster, "magery") - difficulty) / (focus * 2)) / 100
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
    -- A target that cannot be harmed, as an invulnerable or a dead one, takes no curse.
    if not magic.aggress(caster, target, info) then
        return
    end

    -- A curse may ruin the spell its target is casting, and frees a paralyzed target.
    spell.disturb(target)
    mobile.release_paralysis(target)
    mobile.add_stat_curse(target, stat, magic.curse_offset(caster), magic.curse_seconds(caster))
    magic.show(info, target)
end

function magic.protected(who)
    return (mobile.get_prop(who, "magic.armor_until") or 0) > world.now()
end

function magic.protect(caster, who)
    if magic.protected(who) then
        return false
    end

    mobile.set_prop(who, "magic.armor", math.floor(magic.points(caster, "magery") / 10))
    mobile.set_prop(who, "magic.armor_until", world.now() + magic.curse_seconds(caster))

    return true
end

local STATS = { "strength", "dexterity", "intelligence" }

function magic.curse_all(caster, target, info)
    if not magic.aggress(caster, target, info) then
        return
    end

    spell.disturb(target)
    mobile.release_paralysis(target)

    for _, stat in ipairs(STATS) do
        mobile.add_stat_curse(target, stat, magic.curse_offset(caster), magic.curse_seconds(caster))
    end

    magic.show(info, target)
end

function magic.buff(caster, target, info, stats)
    for _, stat in ipairs(stats) do
        mobile.add_stat_bonus(target, stat, magic.curse_offset(caster), magic.curse_seconds(caster), true)
    end

    magic.show(info, target)
end

return magic
