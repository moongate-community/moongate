-- ==============================================================================
-- Moongate - scripts/items/potion.lua
--
-- What it is for:
--   The item script of the potions a player drinks, with script_id = "potion":
--   heal (3-10, 6-20, 9-30 hits, then 10 seconds before another heal potion),
--   refresh (a quarter of the stamina, a total one all of it), strength and
--   agility (+10, greater +20, for 2 minutes) and night sight (15 to 25
--   minutes). Drinking needs the potion in the backpack or within 1 tile and a
--   free hand; one potion goes, an empty bottle comes back. The effect is
--   chosen by the potion's template.
--
-- Functions:
--   potion.on_use(serial, user)   drinks the potion
--   potion.random(low, high)      the roll of a heal, math.random
-- ==============================================================================

potion = {}

potion.random = math.random

local REACH = 1
local TOO_FAR = 502138        -- That is too far away for you to use.
local NO_FREE_HAND = 502172   -- You must have a free hand to drink a potion.
local FULL_HEALTH = 1049547   -- You decide against drinking this potion, as you are already at full health.
local HEAL_WAIT = 500235      -- You must wait 10 seconds before using another healing potion.
local SIMILAR_EFFECT = 502173 -- You are already under a similar effect.
local FULL_STAMINA = "You decide against drinking this potion, as you are already at full stamina."
local HAS_NIGHT_SIGHT = "You already have night sight."

local DRINK_SOUND = 0x2D6
local BOTTLE = "0x0f0e_empty_bottle"
local HEAL_DELAY = 10
local BONUS_SECONDS = 120
local BONUS_EFFECT = 0x375A
local BONUS_SOUND = 0x1E7
local NIGHT_LEVEL = 13
local NIGHT_EFFECT = 0x376A
local NIGHT_SOUND = 0x1E3

-- When each player may drink a heal potion again.
local heal_ready = {}

local function heal(low, high)
    return function(user)
        local stats = mobile.stats(user)

        if stats.hits >= stats.hits_max then
            return FULL_HEALTH
        end

        if (heal_ready[user] or 0) > world.now() then
            return HEAL_WAIT
        end

        heal_ready[user] = world.now() + HEAL_DELAY
        mobile.set_stats(user, { hits = math.min(stats.hits + potion.random(low, high), stats.hits_max) })
    end
end

local function refresh(share)
    return function(user)
        local stats = mobile.stats(user)

        if stats.stamina >= stats.stamina_max then
            return FULL_STAMINA
        end

        mobile.set_stats(user, { stamina = math.min(stats.stamina + math.floor(stats.stamina_max * share), stats.stamina_max) })
    end
end

local function bonus(stat, amount)
    return function(user)
        if not mobile.add_stat_bonus(user, stat, amount, BONUS_SECONDS) then
            return SIMILAR_EFFECT
        end

        effect.on(user, BONUS_EFFECT)
        mobile.play_sound(user, BONUS_SOUND)
    end
end

local function night_sight(user)
    if not mobile.set_night_sight(user, NIGHT_LEVEL, potion.random(15, 25) * 60) then
        return HAS_NIGHT_SIGHT
    end

    effect.on(user, NIGHT_EFFECT)
    mobile.play_sound(user, NIGHT_SOUND)
end

-- What each potion does, by its template: nothing returned is drunk, a client text is a refusal.
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
}

local function tell(user, message)
    if type(message) == "number" then
        mobile.message_cliloc(user, message)
    else
        mobile.message(user, message)
    end
end

local function drink(serial, user)
    mobile.play_sound(user, DRINK_SOUND)

    if mobile.body_type(user) == BodyType.Human and not mobile.is_mounted(user) then
        mobile.animate(user, HumanAnimationType.Eat)
    end

    item.consume(serial, 1)
    item.give(user, BOTTLE)
end

function potion.on_use(serial, user)
    local apply = EFFECTS[item.template(serial) or ""]

    if not apply then
        return true
    end

    if item.owner(serial) ~= user and not item.in_range(serial, user, REACH) then
        mobile.message_cliloc(user, TOO_FAR)

        return true
    end

    if not mobile.has_free_hand(user) then
        mobile.message_cliloc(user, NO_FREE_HAND)

        return true
    end

    local refusal = apply(user)

    if refusal then
        tell(user, refusal)

        return true
    end

    drink(serial, user)

    return true
end
