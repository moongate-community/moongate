-- ==============================================================================
-- Moongate - scripts/spells/create_food.lua
--
-- What it is for:
--   The first circle spell Create Food: a food of a random kind appears in the
--   caster's backpack, or at its feet when the backpack is full, and the caster
--   is told which. It takes no target. Called by the spell service with the
--   caster, a target ({ kind = "none" }) and the data of the spell.
--
-- Functions:
--   create_food.cast(caster, target, info)   the effect, once the cast succeeded
--   create_food.random(low, high)      the pick of the food, math.random
-- ==============================================================================

create_food = {}

create_food.random = math.random

-- What the spell may make: the classic list, by the templates of this server.
local FOODS = {
    { template = "0x09d1_grape_bunch", name = "grapes" },
    { template = "0x09c9_ham", name = "ham" },
    { template = "0x097d_wedge_of_cheese", name = "a wedge of cheese" },
    { template = "0x09eb_muffins", name = "muffins" },
    { template = "0x097b_fish_steak", name = "a fish steak" },
    { template = "0x09b7_cooked_bird", name = "a cooked bird" },
    { template = "0x09c0_sausage", name = "a sausage" },
    { template = "0x09d0_apple", name = "an apple" },
    { template = "0x09d2_peach", name = "a peach" },
}

local SOUND = 0x1E2
local EFFECT = 0x376A
local EFFECT_SPEED = 9
local EFFECT_DURATION = 32
local CREATED = "You magically create food in your backpack: "

function create_food.cast(caster, target, info)
    local food = FOODS[create_food.random(1, #FOODS)]

    if not item.give(caster, food.template) then
        local here = mobile.location(caster)

        if not here or not item.create(food.template, here.map, here.x, here.y, here.z) then
            return
        end
    end

    mobile.message(caster, CREATED .. food.name)
    effect.on(caster, EFFECT, { speed = EFFECT_SPEED, duration = EFFECT_DURATION })
    mobile.play_sound(caster, info.sound ~= 0 and info.sound or SOUND)
end
