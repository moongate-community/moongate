-- ==============================================================================
-- Moongate - scripts/gumps/resurrect.lua
--
-- What it is for:
--   The script of the question of an ankh or of a healer (templates/gumps/
--   resurrect.xml): the Continue button brings the ghost back to life, as
--   ModernUO's ResurrectGump does, with the sound and the sparkles of a
--   resurrection, and it costs a tenth of its fame. A player with five short-term
--   murders or more loses skills and stats too, as ModernUO's TryGiveStatLoss. The ghost may have walked
--   away, or been raised by someone else, while the gump was open.
--
-- Functions:
--   accept(player, response, args)  the Continue button; args.ankh is the ankh,
--                                    args.healer the healer that offered
--   cancel(player, response, args)   the Cancel button: nothing happens
-- ==============================================================================

resurrect = {}

-- How far from the ankh the ghost may stand, in cells; a healer may be farther, as it came to the ghost.
local use_range = 2
local healer_range = 8

-- The fame lost by who comes back: a tenth of it, as ModernUO.
local fame_tenth = 10

-- From how many short-term murders a resurrection costs skills and stats, how much, and what is never taken below.
local murder_limit = 5
local loss_least = 0.85
local loss_most = 0.95
local stat_floor = 10
local skill_floor = 35

-- The fractions of a point are cut off, as ModernUO does, but a product that is whole in decimals is kept whole.
local rounding = 1e-6

-- Client text: "That is too far away."
local too_far_cliloc = 500446

local sound = 0x214

-- Whether the ankh or the healer that asked is still within reach of the ghost.
local function in_reach(player, args)
    if args.healer then
        local at = mobile.location(player)
        local healer_at = npc.location(args.healer)

        return at ~= nil and healer_at ~= nil and healer_at.map == at.map and
            npc.distance_to(args.healer, at.x, at.y) <= healer_range
    end

    return args.ankh ~= nil and item.in_range(args.ankh, player, use_range)
end

-- The name of a skill as mobile.skills gives it, evaluating_intelligence, as the SkillType member mobile.set_skill takes.
local function member_of(name)
    local first = name:sub(1, 1):upper() .. name:sub(2)

    return (first:gsub("_(%l)", function(letter)
        return letter:upper()
    end))
end

-- What is kept of each stat and skill: 95% at five murders, less with more, never under 85%.
local function loss_of(short_term)
    return math.max(loss_least, math.min(loss_most, (100 - (4 + math.floor(short_term / 5))) / 100))
end

-- A murderer comes back poorer: its stats and its skills are multiplied by the loss, and left alone when that would
-- take them under their floor.
local function lose_skills_and_stats(player)
    local murders = mobile.murders(player)

    if murders == nil or murders.short_term < murder_limit then
        return
    end

    local loss = loss_of(murders.short_term)
    local stats = mobile.stats(player)
    local change = {}

    for _, name in ipairs({ "strength", "dexterity", "intelligence" }) do
        local lowered = math.floor(stats[name] * loss + rounding)

        if lowered >= stat_floor then
            change[name] = lowered
        end
    end

    mobile.set_stats(player, change)

    for name, value in pairs(mobile.skills(player)) do
        local lowered = math.floor(value * 10 * loss + rounding) / 10

        if lowered >= skill_floor then
            mobile.set_skill(player, member_of(name), lowered)
        end
    end
end

function resurrect.accept(player, response, args)
    if not mobile.is_dead(player) then
        return
    end

    if not in_reach(player, args) then
        mobile.message_cliloc(player, too_far_cliloc)

        return
    end

    if mobile.resurrect(player) then
        mobile.play_sound(player, sound)
        effect.on(player, EffectGraphicType.SparkleHeal)

        local stats = mobile.stats(player)

        if stats and stats.fame > 0 then
            mobile.set_stats(player, { fame = stats.fame - math.floor(stats.fame / fame_tenth) })
        end

        lose_skills_and_stats(player)
    end
end

function resurrect.cancel(player, response, args)
end
