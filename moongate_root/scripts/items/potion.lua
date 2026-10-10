-- ==============================================================================
-- Moongate - scripts/items/potion.lua
--
-- What it is for:
--   The item script of the potions a player drinks, with script_id = "potion":
--   heal (3-10, 6-20, 9-30 hits, then 10 seconds before another heal potion),
--   refresh (a quarter of the stamina, a total one all of it), strength and
--   agility (+10, greater +20, for 2 minutes) and night sight (15 to 39
--   minutes), poison (lesser to deadly, poisons the drinker) and cure (a
--   chance by the cure and the poison's level). Drinking needs the potion in the backpack or within 1 tile (in a
--   bag on the ground too) and a free hand; one potion goes, then its effect,
--   and an empty bottle comes back, at the feet when the backpack is full. The
--   effect is chosen by the potion's template: each has a check, which may
--   refuse it, and what it does.
--
-- Functions:
--   potion.on_use(serial, user)   drinks the potion
--   potion.random(low, high)      the roll of a heal and of night sight, math.random
--   potion.chance()               the roll of a cure, 0 to 1, math.random
-- ==============================================================================

potion = {}

potion.random = math.random
potion.chance = math.random

local REACH = 1
local TOO_FAR = 502138        -- That is too far away for you to use.
local NO_FREE_HAND = 502172   -- You must have a free hand to drink a potion.
local FULL_HEALTH = 1049547   -- You decide against drinking this potion, as you are already at full health.
local HEAL_WAIT = 500235      -- You must wait 10 seconds before using another healing potion.
local SIMILAR_EFFECT = 502173 -- You are already under a similar effect.
local FULL_STAMINA = "You decide against drinking this potion, as you are already at full stamina."
local HAS_NIGHT_SIGHT = "You already have night sight."
local CANNOT_HEAL = 1005000      -- You can not heal yourself in your current state.
local NOT_POISONED = 1042000     -- You are not poisoned.
local CURED = 500231             -- You feel cured of poison!
local NOT_STRONG_ENOUGH = 500232 -- That potion was not strong enough to cure your ailment!

local DRINK_SOUND = 0x2D6
local BOTTLE = "0x0f0e_empty_bottle"
local HEAL_DELAY = 10
local BONUS_SECONDS = 120
local BONUS_EFFECT = 0x375A
local BONUS_SOUND = 0x1E7
local NIGHT_LEVEL = 13
local NIGHT_EFFECT = 0x376A
local NIGHT_SOUND = 0x1E3
local CURE_EFFECT = 0x373A
local CURE_SOUND = 0x1E0

-- When each player may drink a heal potion again: one more second, so the wait is never under ten.
local heal_ready = {}

local function full_health(user)
    local stats = mobile.stats(user)

    if stats.hits >= stats.hits_max then
        return FULL_HEALTH
    end

    if mobile.poison_level(user) then
        return CANNOT_HEAL
    end

    local ready = heal_ready[user]

    if ready and ready > world.now() then
        return HEAL_WAIT
    end

    heal_ready[user] = nil
end

local function heal(low, high)
    return {
        check = full_health,
        apply = function(user)
            local stats = mobile.stats(user)
            heal_ready[user] = world.now() + HEAL_DELAY + 1
            mobile.set_stats(user, { hits = math.min(stats.hits + potion.random(low, high), stats.hits_max) })
        end,
    }
end

local function refresh(share)
    return {
        check = function(user)
            local stats = mobile.stats(user)

            if stats.stamina >= stats.stamina_max then
                return FULL_STAMINA
            end
        end,
        apply = function(user)
            local stats = mobile.stats(user)
            local gain = math.floor(stats.stamina_max * share)
            mobile.set_stats(user, { stamina = math.min(stats.stamina + gain, stats.stamina_max) })
        end,
    }
end

local function bonus(stat, amount)
    return {
        check = function(user)
            if mobile.stat_bonus(user, stat) > 0 then
                return SIMILAR_EFFECT
            end
        end,
        apply = function(user)
            mobile.add_stat_bonus(user, stat, amount, BONUS_SECONDS)
            effect.on(user, BONUS_EFFECT)
            mobile.play_sound(user, BONUS_SOUND)
        end,
    }
end

local night_sight = {
    check = function(user)
        if mobile.has_night_sight(user) then
            return HAS_NIGHT_SIGHT
        end
    end,
    apply = function(user)
        mobile.set_night_sight(user, NIGHT_LEVEL, potion.random(15, 39) * 60)
        effect.on(user, NIGHT_EFFECT)
        mobile.play_sound(user, NIGHT_SOUND)
    end,
}

-- A poison potion poisons whoever drinks it; a weaker one than the poison at work changes nothing.
local function poison(level)
    return {
        check = function() end,
        apply = function(user)
            mobile.poison(user, level)
        end,
    }
end

-- A cure cures a poison by the chance it has against that poison's level; used up either way.
local function cure(chances)
    return {
        check = function(user)
            if not mobile.poison_level(user) then
                return NOT_POISONED
            end
        end,
        apply = function(user)
            local level = mobile.poison_level(user)

            -- The glow and the sound of a cure, whether it works or not.
            effect.on(user, CURE_EFFECT)
            mobile.play_sound(user, CURE_SOUND)

            if level and potion.chance() < (chances[level] or 0) and mobile.cure(user) then
                mobile.message_cliloc(user, CURED)
            else
                mobile.message_cliloc(user, NOT_STRONG_ENOUGH)
            end
        end,
    }
end

-- What each potion does, by its template.
local EFFECTS = {
    lesserhealpotion = heal(3, 10),
    healpotion = heal(6, 20),
    greaterhealpotion = heal(9, 30),
    refreshmentpotion = refresh(0.25),
    totalrefreshmentpotion = refresh(1.0),
    strengthpotion = bonus("strength", 10),
    greaterstrengthpotion = bonus("strength", 20),
    agilitypotion = bonus("dexterity", 10),
    greateragilitypotion = bonus("dexterity", 20),
    nightsightpotion = night_sight,
    lesserpoisonpotion = poison(0),
    poisonpotion = poison(1),
    greaterpoisonpotion = poison(2),
    deadlypoisonpotion = poison(3),
    -- The chance against lesser (0), regular (1), greater (2), deadly (3) and lethal (4) poison.
    lessercurepotion = cure({ [0] = 0.75, [1] = 0.50, [2] = 0.15 }),
    curepotion = cure({ [0] = 1.00, [1] = 0.75, [2] = 0.50, [3] = 0.15 }),
    greatercurepotion = cure({ [0] = 1.00, [1] = 1.00, [2] = 1.00, [3] = 0.75, [4] = 0.25 }),
}

local function tell(user, message)
    if type(message) == "number" then
        mobile.message_cliloc(user, message)
    else
        mobile.message(user, message)
    end
end

-- The item on the ground the potion is in, or the potion itself: a potion in a bag within a tile is in reach.
local function ground_root(serial)
    local root = serial

    while item.container(root) do
        root = item.container(root)
    end

    return root
end

local function within_reach(serial, user)
    return item.owner(serial) == user or item.in_range(ground_root(serial), user, REACH)
end

local function leave_bottle(user)
    if item.give(user, BOTTLE) then
        return
    end

    local here = mobile.location(user)

    if here then
        item.create(BOTTLE, here.map, here.x, here.y, here.z)
    end
end

function potion.on_use(serial, user)
    local effect_of = EFFECTS[item.template(serial) or ""]

    if not effect_of then
        return true
    end

    if not within_reach(serial, user) then
        mobile.message_cliloc(user, TOO_FAR)

        return true
    end

    if not mobile.has_free_hand(user) then
        mobile.message_cliloc(user, NO_FREE_HAND)

        return true
    end

    local refusal = effect_of.check(user)

    if refusal then
        tell(user, refusal)

        return true
    end

    -- Used up first: a potion that cannot be, such as one held on a cursor, does nothing.
    if not item.consume(serial, 1) then
        return true
    end

    effect_of.apply(user)
    mobile.play_sound(user, DRINK_SOUND)

    if mobile.body_type(user) == BodyType.Human and not mobile.is_mounted(user) then
        mobile.animate(user, HumanAnimationType.Eat)
    end

    leave_bottle(user)

    return true
end
