-- ==============================================================================
-- Moongate - scripts/items/food.lua
--
-- What it is for:
--   The item script of what can be eaten, as ModernUO's Food: double clicking it
--   eats one. The player gets less hungry (mobile.hunger, from 0 to 20), gets a
--   little stamina back, makes the sound and the gesture of eating and reads how
--   full it feels, in the language of its client. A player that is full is told
--   so and eats nothing. An item template uses it with script_id = "food".
--
-- Props it reads:
--   food.fill   how much one piece fills, from 1 up; 3 without it
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the food, carried or
--                          on the ground within 2 tiles; returns true
-- ==============================================================================

food = {}

-- How full a mobile can be, and how much a piece fills when its item does not say.
local FULL = 20
local FILL = 3

-- The texts of the client (ModernUO's): too full, then how the player feels after eating.
local TOO_FULL = 500867
local FEELS = {
    { below = 5, text = 500868 },
    { below = 10, text = 500869 },
    { below = 15, text = 500870 },
    { below = FULL, text = 500871 }
}
local STUFFED = 500872

local function fill_of(serial)
    local fill = math.floor(tonumber(item.get_prop(serial, "food.fill")) or FILL)

    return math.max(fill, 1)
end

-- Called when a player double clicks the food.
function food.on_use(serial, user)
    local hunger = mobile.hunger(user)

    if hunger == nil then
        return true
    end

    if hunger >= FULL then
        mobile.message_cliloc(user, TOO_FULL)

        return true
    end

    local fill = fill_of(serial)

    -- Held on a cursor, or gone in the meantime: nothing is eaten.
    if not item.consume(serial, 1) then
        return true
    end

    mobile.play_sound(user, 0x3A + math.random(0, 2))

    local stats = mobile.stats(user)

    -- Only a human body, of any race, has the gesture of eating.
    if mobile.body_type(user) == BodyType.Human then
        mobile.animate(user, HumanAnimationType.Eat)
    end

    if stats.stamina < stats.stamina_max then
        local gain = math.random(6, 8) + math.floor(fill / 5)
        mobile.set_stats(user, { stamina = math.min(stats.stamina + gain, stats.stamina_max) })
    end

    hunger = math.min(hunger + fill, FULL)
    mobile.set_hunger(user, hunger)

    local text = STUFFED

    for _, feel in ipairs(FEELS) do
        if hunger < feel.below then
            text = feel.text

            break
        end
    end

    mobile.message_cliloc(user, text)

    return true
end
