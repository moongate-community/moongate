-- ==============================================================================
-- Moongate - scripts/items/ore.lua
--
-- What it is for:
--   The item script of the piles of ore: a player double clicks a pile it
--   carries, or one lying within 2 tiles, picks a forge within 2 tiles, and the
--   whole pile is smelted into ingots of its metal (scripts/common/metals.lua).
--   A template uses it with script_id = "ore".
--
--   The Mining skill is tried 25 below and above the metal's difficulty: iron
--   between 25 and 75, dull copper between 40 and 90, up to valorite between
--   74 and 124; below that a smelt always fails, above it always works, and
--   the try may raise the skill. A metal other than iron is not tried at all
--   by a miner below its difficulty: "You have no idea how to smelt this
--   strange ore!", and nothing burns.
--     works   every ore of the pile becomes ingots, by the size of the pile:
--             a large pile gives 2 ingots for each ore, a medium one 1, a small
--             one 1 for every 2 ore (an odd one is left).
--     fails   half the pile is burnt away, rounded down; a pile of one iron ore
--             gets smaller instead: a large one becomes medium, a medium one
--             small. A single ore of another metal is burnt away.
--   A single small ore is too little to smelt. The ore is taken before the
--   ingots are given, so a backpack with no room for them loses the metal, and a
--   pile a player holds on its cursor is not smelted.
--
--   A forge is an item or a part of the map with one of the graphics below.
--
-- Functions:
--   on_use(serial, user)   the player user double clicks the pile serial
-- ==============================================================================

local smithy = require("common.smithy")
local metals = require("common.metals")

ore = {}

-- How far the forge may be, in tiles.
local RANGE = 2

-- How far below and above its difficulty a metal is tried.
local SMELT_SPREAD = 25

-- The graphics of the piles.
local SMALL = 0x19B7
local MEDIUM = 0x19B8
local LARGE = 0x19B9

-- The sound of a smelt.
local SMELT_SOUND = 0x2B

-- Client texts.
local WHICH_FORGE = 501971    -- Select the forge on which to smelt the ore, or another pile of ore with which to combine it.
local ORE_TOO_FAR = 501976    -- The ore is too far away.
local TOO_LITTLE = 501987     -- There is not enough metal-bearing ore in this pile to make an ingot.
local SMELTED = 501988        -- You smelt the ore removing the impurities and put the metal in your backpack.
local BURNT = 501990          -- You burn away the impurities but are left with less useable metal.
local STRANGE_ORE = 501986    -- You have no idea how to smelt this strange ore!
local TOO_FAR = 500446        -- That is too far away.
local NOT_A_FORGE = "That is not a forge."
local NO_ROOM = "You have no room in your backpack for the ingots: the metal is lost."

local function is_forge(graphic)
    return smithy.is_forge(graphic)
end

-- The most a stack holds: more ingots than that are given as several stacks.
local MAX_STACK = 60000

-- Whether the player can still reach the pile: carried, or lying within 2 tiles, and on nobody's cursor. A pile
-- lifted onto a cursor still counts where it was, and cannot be taken from: it must not be smelted.
local function has_pile(pile, user)
    return not item.is_held(pile) and (item.owner(pile) == user or item.in_range(pile, user, 2))
end

-- The template of a pile by its graphic.
local PILES = {
    [SMALL] = "0x19b7_iron_ore",
    [MEDIUM] = "0x19b8_iron_ore",
}

-- Whether what the player picked is a forge within reach; false with the reason told.
local function forge_picked(user, picked)
    local here = mobile.location(user)

    if picked.kind == "object" then
        local graphic = item.item_id(picked.serial)

        if not graphic or not is_forge(graphic) then
            mobile.message(user, NOT_A_FORGE)

            return false
        end

        if not item.in_range(picked.serial, user, RANGE) then
            mobile.message_cliloc(user, TOO_FAR)

            return false
        end

        return true
    end

    if not is_forge(picked.graphic or 0) then
        mobile.message(user, NOT_A_FORGE)

        return false
    end

    if here.map ~= picked.map or math.abs(here.x - picked.x) > RANGE or math.abs(here.y - picked.y) > RANGE then
        mobile.message_cliloc(user, TOO_FAR)

        return false
    end

    return true
end

-- How many ingots a pile of that graphic and amount gives, and how many ore are left over.
local function ingots_of(graphic, amount)
    if graphic == LARGE then
        return amount * 2, 0
    end

    if graphic == SMALL then
        return math.floor(amount / 2), amount % 2
    end

    return amount, 0
end

-- The player picked the forge.
local function smelt(pile, user, picked)
    if picked.kind == "canceled" then
        return
    end

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) then
        return
    end

    local graphic, amount = item.item_id(pile), item.amount(pile)

    if not graphic or not amount or not has_pile(pile, user) then
        mobile.message_cliloc(user, ORE_TOO_FAR)

        return
    end

    if not forge_picked(user, picked) then
        return
    end

    local metal = metals.of_ore(item.template(pile)) or metals.iron
    local ingots, left = ingots_of(graphic, amount)

    if ingots < 1 then
        mobile.message_cliloc(user, TOO_LITTLE)

        return
    end

    -- A metal above the miner's skill is not even tried: nothing burns, and nothing is learnt from it.
    if metal ~= metals.iron and (mobile.skills(user).mining or 0) < metal.smelt then
        mobile.message_cliloc(user, STRANGE_ORE)

        return
    end

    if not skill.check(user, "mining", metal.smelt - SMELT_SPREAD, metal.smelt + SMELT_SPREAD) then
        -- Half the pile is lost; a single ore gets smaller instead: it is taken, and a smaller one given.
        local lost = amount > 1 and amount - math.floor(amount / 2) or 1

        if not item.consume(pile, lost) then
            mobile.message_cliloc(user, ORE_TOO_FAR)

            return
        end

        -- Only iron comes in smaller piles: a single ore of another metal is simply gone.
        if amount == 1 and metal == metals.iron then
            item.give(user, PILES[graphic == LARGE and MEDIUM or SMALL])
        end

        mobile.message_cliloc(user, BURNT)

        return
    end

    -- The ore is taken before the ingots are given: ingots never come from ore that stayed.
    if not item.consume(pile, amount - left) then
        mobile.message_cliloc(user, ORE_TOO_FAR)

        return
    end

    mobile.play_sound(user, SMELT_SOUND)

    while ingots > 0 do
        local stack = math.min(ingots, MAX_STACK)

        -- A backpack with no room loses the metal, as it loses the ore of a dig.
        if not item.give(user, metal.ingot, stack) then
            mobile.message(user, NO_ROOM)

            return
        end

        ingots = ingots - stack
    end

    mobile.message_cliloc(user, SMELTED)
end

-- Called when a player double clicks the pile.
function ore.on_use(serial, user)
    -- A pile inside a chest on the ground is used through the chest: it must be taken out first.
    if not has_pile(serial, user) then
        mobile.message_cliloc(user, ORE_TOO_FAR)

        return true
    end

    mobile.message_cliloc(user, WHICH_FORGE)

    target.pick_location(user, function(picked)
        smelt(serial, user, picked)
    end)

    return true
end
