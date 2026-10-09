-- ==============================================================================
-- Moongate - scripts/common/crafting.lua
--
-- What it is for:
--   The rules every craft shares, used with
--   local crafting = require("common.crafting") by the tool scripts and the
--   crafting gump (scripts/gumps/craft.lua). The recipes are data
--   (data/crafts, read with the craft module); the kinds of wood are
--   scripts/common/woods.lua.
--
--   One attempt: the main skill and any other skill of the recipe must be at
--   least their least; a kind of wood picked must not ask for more of the
--   craft's skill than the player has; every resource must be carried, in
--   the backpack or a bag of it (not the bank box, not a pile on the cursor).
--   Then two strokes 1.25 seconds apart, each with the craft's sound. At the
--   second everything is checked again, every other skill of the recipe is
--   tried, and the main skill is tried between twice its least minus its
--   most and its most: the chance is one in two at the least and grows in a
--   line to sure at the most, and the skill may rise.
--   Success takes every resource and makes the item in the backpack, with
--   the colour of the kind of wood; a full backpack still takes them and
--   puts the item at the player's feet. Failure takes half of each resource,
--   rounded down. The gump opens again with what happened.
--
-- Functions:
--   crafting.open(user, tool, craft_id, notice)   opens the crafting gump
--   crafting.make(user, tool, craft_id, group, recipe)   one attempt
--   crafting.kind(user) / crafting.set_kind(user, id)   the wood picked
--   crafting.group(user) / crafting.set_group(user, index)   the group shown
--   crafting.templates(resource, kind)   the templates that count
--   crafting.count(user, templates)   how many units the player carries
--   crafting.chance(user, craft, recipe)   the chance, 0 to 1
--   crafting.notice(cliloc)   the text the gump shows for a client text
--   crafting.carries(user, tool)   whether the tool is in the backpack or a bag of it
--
-- What it keeps:
--   Who is making something, and the group and wood each player picked, in
--   memory by serial, not saved: a restart goes back to plain wood.
-- ==============================================================================

local woods = require("common.woods")

local crafting = {}

-- Between the two strokes, in seconds.
local STROKE = 1.25

-- Client texts, and the text the gump shows for each.
local CREATED = 1044154       -- You create the item.
local FAILED = 1044043        -- You failed to create the item, and some of your materials are lost.
local NO_SKILL = 1044153      -- You don't have the required skills to attempt this item.
local NO_WOOD = 1044351       -- You do not have sufficient wood to make that.
local NO_CLOTH = 1044287      -- You don't have enough cloth to make that.
local NO_COMPONENTS = 1044253 -- You don't have the components needed to make that.
local STRANGE_WOOD = 1072652  -- You cannot work this strange and unusual wood.
local BUSY = 500119           -- You must wait to perform another action.

local NOTICES = {
    [CREATED] = "You create the item.",
    [FAILED] = "You failed to create the item, and some of your materials are lost.",
    [NO_SKILL] = "You don't have the required skills to attempt this item.",
    [NO_WOOD] = "You do not have sufficient wood to make that.",
    [NO_CLOTH] = "You don't have enough cloth to make that.",
    [NO_COMPONENTS] = "You don't have the components needed to make that.",
    [STRANGE_WOOD] = "You cannot work this strange and unusual wood.",
}

local MISSING = { wood = NO_WOOD, cloth = NO_CLOTH }

local AT_YOUR_FEET = "Your backpack is full: the item is at your feet."

-- Who is making something, the wood and the group each player picked.
local busy = {}
local kinds = {}
local groups = {}

function crafting.notice(cliloc)
    return NOTICES[cliloc]
end

function crafting.open(user, tool, craft_id, notice)
    gump.open(user, "craft_menu", { craft = craft_id, tool = tool, notice = notice })
end

function crafting.kind(user)
    return kinds[user] or "plain"
end

local function points(user, skill)
    return (mobile.skills(user) or {})[skill] or 0
end

function crafting.set_kind(user, id, craft_skill)
    local kind = woods.by_id(id)

    if not kind then
        return false
    end

    if points(user, craft_skill or "carpentry") < kind.carpentry then
        mobile.message_cliloc(user, STRANGE_WOOD)

        return false
    end

    kinds[user] = kind.id

    return true
end

function crafting.group(user)
    return groups[user] or 1
end

function crafting.set_group(user, index)
    groups[user] = index
end

function crafting.templates(resource, kind)
    if resource == "wood" and kind and kind ~= "plain" then
        local wood = woods.by_id(kind)

        return wood and { wood.boards } or {}
    end

    return craft.resource(resource) or { resource }
end

-- The stacks of those templates the player carries, a pile on the cursor left out.
local function stacks(user, templates)
    local found = {}

    for _, template in ipairs(templates) do
        for _, serial in ipairs(item.find(user, template)) do
            if not item.is_held(serial) then
                found[#found + 1] = serial
            end
        end
    end

    return found
end

function crafting.count(user, templates)
    local total = 0

    for _, serial in ipairs(stacks(user, templates)) do
        total = total + (item.amount(serial) or 0)
    end

    return total
end

-- Whether every skill of the recipe is at its least.
local function skilled(user, craft, recipe)
    if points(user, craft.skill) < recipe.skill_min then
        return false
    end

    for _, other in ipairs(recipe.skills) do
        if points(user, other.skill) < other.min then
            return false
        end
    end

    return true
end

function crafting.chance(user, craft, recipe)
    if not skilled(user, craft, recipe) then
        return 0
    end

    if recipe.skill_max <= recipe.skill_min then
        return 1
    end

    local chance = 0.5 + 0.5 * (points(user, craft.skill) - recipe.skill_min) / (recipe.skill_max - recipe.skill_min)

    return math.max(0, math.min(1, chance))
end

-- The first resource the player lacks, as its client text; nil when all are there.
local function missing(user, recipe, kind)
    for _, resource in ipairs(recipe.resources) do
        if crafting.count(user, crafting.templates(resource.resource, kind)) < resource.amount then
            return MISSING[resource.resource] or NO_COMPONENTS
        end
    end

    return nil
end

-- Takes amount units from the stacks, in turn; gives the hue of the first stack taken from.
local function take(user, templates, amount)
    local hue

    for _, serial in ipairs(stacks(user, templates)) do
        if amount <= 0 then
            break
        end

        local here = item.amount(serial) or 0
        local taken = math.min(here, amount)

        if taken > 0 then
            hue = hue or item.hue(serial)

            if item.consume(serial, taken) then
                amount = amount - taken
            end
        end
    end

    return hue
end

function crafting.carries(user, tool)
    for _, serial in ipairs(item.find(user, item.template(tool) or "")) do
        if serial == tool then
            return true
        end
    end

    return false
end

-- The second stroke: the result.
local function finish(user, tool, craft_id, craft, recipe, kind)
    busy[user] = nil

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) or not crafting.carries(user, tool) then
        return
    end

    local lacking = missing(user, recipe, kind)

    if lacking then
        mobile.message_cliloc(user, lacking)
        crafting.open(user, tool, craft_id, NOTICES[lacking])

        return
    end

    mobile.play_sound(user, craft.sound)

    for _, other in ipairs(recipe.skills) do
        skill.check(user, other.skill, other.min, other.max)
    end

    -- Twice the least minus the most: one in two at the least, sure at the most.
    local passed = skill.check(user, craft.skill, 2 * recipe.skill_min - recipe.skill_max, recipe.skill_max)

    if not passed then
        for _, resource in ipairs(recipe.resources) do
            take(user, crafting.templates(resource.resource, kind), math.floor(resource.amount / 2))
        end

        mobile.message_cliloc(user, FAILED)
        crafting.open(user, tool, craft_id, NOTICES[FAILED])

        return
    end

    local hue

    for _, resource in ipairs(recipe.resources) do
        local taken = take(user, crafting.templates(resource.resource, kind), resource.amount)

        if resource.resource == "wood" and kind ~= "plain" then
            hue = taken
        end
    end

    local made = item.give(user, recipe.item)

    if not made then
        made = item.create(recipe.item, here.map, here.x, here.y, here.z)

        if made then
            mobile.message(user, AT_YOUR_FEET)
        end
    end

    if made and hue and hue ~= 0 then
        item.set_hue(made, hue)
    end

    mobile.message_cliloc(user, CREATED)
    crafting.open(user, tool, craft_id, NOTICES[CREATED])
end

function crafting.make(user, tool, craft_id, group, index)
    if busy[user] then
        mobile.message_cliloc(user, BUSY)

        return
    end

    local craft = craft_id and _G.craft.get(craft_id)
    local recipe = craft and craft.groups[group] and craft.groups[group].recipes[index]

    if not recipe then
        return
    end

    local kind = crafting.kind(user)

    if not skilled(user, craft, recipe) then
        mobile.message_cliloc(user, NO_SKILL)
        crafting.open(user, tool, craft_id, NOTICES[NO_SKILL])

        return
    end

    local wood = woods.by_id(kind)

    if wood and points(user, craft.skill) < wood.carpentry then
        mobile.message_cliloc(user, STRANGE_WOOD)
        crafting.open(user, tool, craft_id, NOTICES[STRANGE_WOOD])

        return
    end

    local lacking = missing(user, recipe, kind)

    if lacking then
        mobile.message_cliloc(user, lacking)
        crafting.open(user, tool, craft_id, NOTICES[lacking])

        return
    end

    busy[user] = true
    mobile.play_sound(user, craft.sound)

    timer.after(STROKE, function()
        finish(user, tool, craft_id, craft, recipe, kind)
    end)
end

return crafting
