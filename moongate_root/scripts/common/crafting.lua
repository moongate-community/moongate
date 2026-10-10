-- ==============================================================================
-- Moongate - scripts/common/crafting.lua
--
-- What it is for:
--   The rules every craft shares, used with
--   local crafting = require("common.crafting") by the tool scripts and the
--   crafting gump (scripts/gumps/craft_menu.lua). The recipes are data
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
--   Success makes the item, in the backpack or, when it has no room, at the
--   player's feet, with the colour of the kind of wood, and then takes every
--   resource: an item that cannot be made takes nothing, and resources that
--   cannot all be taken make nothing. Failure takes half of each resource,
--   rounded down, and at least one unit of the first. The gump opens again
--   with what happened.
--
--   A success is exceptional as often as its chance minus six tenths (at the
--   most of a recipe, four times in ten): the item's prop quality is 2. Made
--   at 100 of the main skill, an exceptional item bears its maker's mark, the
--   props crafter_id and crafter_name, which its tooltip shows.
--
--   A tool lasts 25 to 75 uses, drawn the first time it is used (its prop
--   uses_remaining); every attempt whose skill is tried takes one, and the
--   last one breaks it. An item that joins a stack the player carries is
--   never exceptional: the stack is not. An exceptional item is uncommon, a
--   marked one rare.
--
--   A craft may ask to stand near things (the table NEEDS): blacksmithing an
--   anvil and a forge within 2 tiles, the baking of cooking an oven and its
--   barbecue a fire, checked when the attempt starts and at its second
--   stroke. Make last starts again the last recipe the player started with
--   that craft.
--
-- Functions:
--   crafting.open(user, tool, craft_id, notice)   opens the crafting gump
--   crafting.make(user, tool, craft_id, group, recipe)   one attempt
--   crafting.kind(user, craft_id) / crafting.set_kind(user, id, craft_id)   the kind of wood or metal picked, by craft;
--     set_kind gives false and the client text when the player cannot work it
--   crafting.material_label(material)   "Wood" or "Metal", as the gump shows it
--   crafting.material(craft) / crafting.kinds(craft)   the material a craft works in kinds, and its kinds
--   crafting.group(user, craft_id) / crafting.set_group(user, index, craft_id)   the group shown, by craft
--   crafting.takes(recipe, material)   whether a recipe takes that material
--   crafting.templates(resource, kind)   the templates that count
--   crafting.count(user, templates)   how many units the player carries
--   crafting.chance(user, craft, recipe)   the chance, 0 to 1
--   crafting.make_last(user, tool, craft_id)   the last recipe again
--   crafting.uses(tool)   the uses left of a tool, drawn the first time
--   crafting.roll()   a number from 0 up to 1, drawn for the exceptional items and the uses
--   crafting.notice(cliloc)   the text the gump shows for a client text
--   crafting.carries(user, tool)   whether the tool is in the backpack or a bag of it
--
-- What it keeps:
--   Who is making something, the group and wood each player picked and the
--   last recipe each started, in memory by serial, not saved: a restart goes
--   back to plain wood. The uses of a tool are a prop of the tool, saved.
-- ==============================================================================

local woods = require("common.woods")
local smithy = require("common.smithy")
local heat = require("common.heat")
local metals = require("common.metals")

local crafting = {}

-- Between the two strokes, in seconds.
local STROKE = 1.25

-- How long after its start an attempt whose second stroke never came no longer holds the player, in seconds.
local GIVE_UP = 10

-- Client texts, and the text the gump shows for each.
local CREATED = 1044154       -- You create the item.
local FAILED = 1044043        -- You failed to create the item, and some of your materials are lost.
local NO_SKILL = 1044153      -- You don't have the required skills to attempt this item.
local NO_WOOD = 1044351       -- You do not have sufficient wood to make that.
local NO_CLOTH = 1044287      -- You don't have enough cloth to make that.
local NO_COMPONENTS = 1044253 -- You don't have the components needed to make that.
local STRANGE_WOOD = 1072652  -- You cannot work this strange and unusual wood.
local BUSY = 500119           -- You must wait to perform another action.
local EXCEPTIONAL = 1044155   -- You create an exceptional quality item.
local MARKED = 1044156        -- You create an exceptional quality item and affix your maker's mark.
local WORN_OUT = 1044038      -- You have worn out your tool!
local NOTHING_YET = 1044165   -- You haven't made anything yet.
local NO_METAL = 1044037      -- You do not have sufficient metal to make that.
local NO_LEATHER = 1044463    -- You do not have sufficient leather to make that.
local NO_BONE = 1049063       -- You do not have enough bones to make that.
local NOT_AT_FORGE = 1044267  -- You must be near an anvil and a forge to smith items.
local NOT_AT_FIRE = 1044487   -- You must be near a fire source to cook.
local NOT_AT_OVEN = 1044493   -- You must be near an oven to bake that.
local NO_IDEA_METAL = 1044268 -- You have no idea how to work this metal.

-- How much better than sure a success must be to be exceptional, and the skill that marks it.
local EXCEPTIONAL_MARGIN = 0.6
local MARK_SKILL = 100
local EXCEPTIONAL_QUALITY = 2

-- The uses of a tool, drawn the first time it is used.
local USES_MIN = 25
local USES_MAX = 75

local NOTICES = {
    [CREATED] = "You create the item.",
    [EXCEPTIONAL] = "You create an exceptional quality item.",
    [MARKED] = "You create an exceptional quality item and affix your maker's mark.",
    [NOTHING_YET] = "You haven't made anything yet.",
    [NO_METAL] = "You do not have sufficient metal to make that.",
    [NO_LEATHER] = "You do not have sufficient leather to make that.",
    [NO_BONE] = "You do not have enough bones to make that.",
    [NOT_AT_FORGE] = "You must be near an anvil and a forge to smith items.",
    [NOT_AT_FIRE] = "You must be near a fire source to cook.",
    [NOT_AT_OVEN] = "You must be near an oven to bake that.",
    [NO_IDEA_METAL] = "You have no idea how to work this metal.",
    [FAILED] = "You failed to create the item, and some of your materials are lost.",
    [NO_SKILL] = "You don't have the required skills to attempt this item.",
    [NO_WOOD] = "You do not have sufficient wood to make that.",
    [NO_CLOTH] = "You don't have enough cloth to make that.",
    [NO_COMPONENTS] = "You don't have the components needed to make that.",
    [STRANGE_WOOD] = "You cannot work this strange and unusual wood.",
}

local MISSING = { wood = NO_WOOD, cloth = NO_CLOTH, metal = NO_METAL, leather = NO_LEATHER, bone = NO_BONE }

-- The materials a craft works in kinds, by the resource that takes them: the module of the kinds, the kind picked when
-- none is, the field of a kind with its template, the field with the skill it asks of the craft, and the client
-- text when the skill is lacking. Every craft that works wood (bowcraft too) asks a kind's carpentry, every craft that
-- works metal (tinkering too) a kind's blacksmithy: the same minimums.
local MATERIALS = {
    wood = { module = woods, default = "plain", template = "boards", skill = "carpentry", cannot = STRANGE_WOOD, label = "Wood" },
    metal = { module = metals, default = "iron", template = "ingot", skill = "blacksmithy", cannot = NO_IDEA_METAL, label = "Metal" },
}

-- What a craft asks to stand near, by its id: a test of the player, and the client text when it fails.
local NEEDS = {
    blacksmithing = { test = smithy.at_anvil_and_forge, missing = NOT_AT_FORGE },
    -- Cooking asks it of some groups only: the doughs are mixed anywhere.
    cooking = {
        groups = {
            Baking = { test = heat.at_oven, missing = NOT_AT_OVEN },
            Barbecue = { test = heat.at_fire, missing = NOT_AT_FIRE },
        },
    },
}

-- The client text of what the player is not near for that craft and group; nil when nothing is missing.
local function not_near(user, craft_id, group)
    local need = NEEDS[craft_id]

    if need and need.groups then
        need = need.groups[group]
    end

    if need and not need.test(user) then
        return need.missing
    end

    return nil
end

local AT_YOUR_FEET = "Your backpack is full: the item is at your feet."
local NOT_MADE = "The item could not be made."

-- Who is making something, until when; the kind of wood or metal and the group each player picked, by craft.
local busy = {}
local kinds = {}
local groups = {}
-- The last recipe each player started, by craft.
local last = {}

-- A number from 0 up to 1.
crafting.roll = math.random

function crafting.uses(tool)
    -- A prop a script wrote as anything but a number is drawn again.
    local left = tonumber(item.get_prop(tool, "uses_remaining"))

    if left == nil then
        left = math.min(USES_MAX, USES_MIN + math.floor(crafting.roll() * (USES_MAX - USES_MIN + 1)))
        item.set_prop(tool, "uses_remaining", left)
    end

    return left
end

-- Takes one use of the tool; false, with the client's text, when that was its last and it broke. Making a stackable item
-- (an arrow, a shaft) takes none: UOX3 made them by the fifty for one use.
local function wear(user, tool, plain)
    if plain then
        return true
    end

    local left = crafting.uses(tool) - 1

    if left > 0 then
        item.set_prop(tool, "uses_remaining", left)

        return true
    end

    item.delete(tool)
    mobile.message_cliloc(user, WORN_OUT)

    return false
end

function crafting.notice(cliloc)
    return NOTICES[cliloc]
end

function crafting.open(user, tool, craft_id, notice)
    gump.open(user, "craft_menu", { craft = craft_id, tool = tool, notice = notice })
end

local function points(user, skill)
    return (mobile.skills(user) or {})[skill] or 0
end

-- The material the recipes of a craft take in kinds ("wood", "metal"), or nil.
function crafting.material(data)
    for _, group in ipairs(data and data.groups or {}) do
        for _, recipe in ipairs(group.recipes) do
            for _, resource in ipairs(recipe.resources) do
                if MATERIALS[resource.resource] then
                    return resource.resource
                end
            end
        end
    end

    return nil
end

-- The label of a material in the gump, such as "Wood".
function crafting.material_label(material)
    return MATERIALS[material] and MATERIALS[material].label
end

-- The kinds of the material of a craft, the default first, each with its id and name.
function crafting.kinds(data)
    local material = MATERIALS[crafting.material(data)]

    if not material then
        return {}
    end

    local list = { material.module.by_id(material.default) }

    for _, kind in ipairs(material.module.kinds) do
        list[#list + 1] = kind
    end

    return list
end

-- The kind a player picked for a craft; the material's default when none was picked.
function crafting.kind(user, craft_id)
    local material = MATERIALS[crafting.material(craft.get(craft_id or ""))]
    local picked = (kinds[user] or {})[craft_id or ""]

    -- A kind the material no longer knows, after its data changed, is the default again.
    if picked and material and material.module.by_id(picked) then
        return picked
    end

    return material and material.default or "plain"
end

function crafting.set_kind(user, id, craft_id)
    local data = craft.get(craft_id or "")
    local material = MATERIALS[crafting.material(data)]
    local kind = material and material.module.by_id(id)

    if not kind then
        return false
    end

    if points(user, data.skill) < (kind[material.skill] or 0) then
        mobile.message_cliloc(user, material.cannot)

        return false, material.cannot
    end

    kinds[user] = kinds[user] or {}
    kinds[user][craft_id] = kind.id

    return true
end

-- The group each player shows, by craft.
function crafting.group(user, craft_id)
    return (groups[user] or {})[craft_id or ""] or 1
end

function crafting.set_group(user, index, craft_id)
    groups[user] = groups[user] or {}
    groups[user][craft_id or ""] = index
end

-- Whether a recipe takes that material.
function crafting.takes(recipe, material)
    for _, resource in ipairs(recipe.resources) do
        if resource.resource == material then
            return true
        end
    end

    return false
end

function crafting.templates(resource, kind)
    -- A kind of another material, such as the oak picked for a recipe that also takes metal, leaves this one plain.
    local material = MATERIALS[resource]
    local picked = material and kind and kind ~= material.default and material.module.by_id(kind)

    if picked then
        return { picked[material.template] }
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

-- Takes amount units from the stacks, in turn; gives how many it could not take.
local function take(user, templates, amount)
    for _, serial in ipairs(stacks(user, templates)) do
        if amount <= 0 then
            break
        end

        local taken = math.min(item.amount(serial) or 0, amount)

        if taken > 0 and item.consume(serial, taken) then
            amount = amount - taken
        end
    end

    return amount
end

-- The hue of the first stack that would be taken, for an item made of a kind of wood.
local function hue_of(user, templates)
    local first = stacks(user, templates)[1]

    return first and item.hue(first) or 0
end

-- Makes the item in the backpack, coloured before it looks for a stack so it joins only one of its colour, or at the
-- feet when the backpack has no room. Gives its serial and whether it is at the feet; nil when it cannot be made.
local function make_item(user, template, hue, here)
    local made = item.give(user, template, nil, hue ~= 0 and hue or nil)

    if made then
        return made, false
    end

    made = item.create(template, here.map, here.x, here.y, here.z)

    if made and hue ~= 0 then
        item.set_hue(made, hue)
    end

    return made, made ~= nil
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
local function finish(user, tool, craft_id, craft, group, recipe, kind)
    busy[user] = nil
    -- A stackable item (arrows, shafts) is plain: no quality and no mark, so every one of its colour stacks.
    local plain = item.is_stackable(recipe.item)

    local here = mobile.location(user)

    if not here or mobile.is_dead(user) or not crafting.carries(user, tool) then
        return
    end

    local lacking = not_near(user, craft_id, group) or missing(user, recipe, kind)

    if lacking then
        mobile.message_cliloc(user, lacking)
        crafting.open(user, tool, craft_id, NOTICES[lacking])

        return
    end

    mobile.play_sound(user, craft.sound)

    -- The chance and the mark as they were before this try could raise the skill.
    local chance = crafting.chance(user, craft, recipe)
    local marks = points(user, craft.skill) >= MARK_SKILL

    for _, other in ipairs(recipe.skills) do
        skill.check(user, other.skill, other.min, other.max)
    end

    -- Twice the least minus the most: one in two at the least, sure at the most.
    local passed = skill.check(user, craft.skill, 2 * recipe.skill_min - recipe.skill_max, recipe.skill_max)

    if not passed then
        for index, resource in ipairs(recipe.resources) do
            -- At least one unit of the first: a failure that takes nothing would be a free try of the skill.
            local lost = math.floor(resource.amount / 2)

            take(user, crafting.templates(resource.resource, kind), index == 1 and math.max(1, lost) or lost)
        end

        mobile.message_cliloc(user, FAILED)

        if wear(user, tool, plain) then
            crafting.open(user, tool, craft_id, NOTICES[FAILED])
        end

        return
    end

    local hue = 0

    -- The colour of the kind picked, from the stack it takes.
    for _, resource in ipairs(recipe.resources) do
        local material = MATERIALS[resource.resource]

        if material and kind ~= material.default and material.module.by_id(kind) then
            hue = hue_of(user, crafting.templates(resource.resource, kind))
        end
    end

    -- The stacks of the item already carried: one the new item joins is never made exceptional.
    local carried = {}

    for _, serial in ipairs(item.find(user, recipe.item)) do
        carried[serial] = true
    end

    -- The item first, then the resources: what cannot be made takes nothing, and what cannot be paid is not kept.
    local made, at_feet = make_item(user, recipe.item, hue, here)
    local joined = made and carried[made]

    if not made then
        mobile.message(user, NOT_MADE)

        if wear(user, tool, plain) then
            crafting.open(user, tool, craft_id, NOT_MADE)
        end

        return
    end

    for _, resource in ipairs(recipe.resources) do
        if take(user, crafting.templates(resource.resource, kind), resource.amount) > 0 then
            -- Only the unit just made goes: never a stack the player had.
            if joined then
                item.consume(made, 1)
            else
                item.delete(made)
            end

            mobile.message(user, NOT_MADE)

            if wear(user, tool, plain) then
                crafting.open(user, tool, craft_id, NOT_MADE)
            end

            return
        end
    end

    if at_feet then
        mobile.message(user, AT_YOUR_FEET)
    end

    local outcome = CREATED

    if not joined and not plain and crafting.roll() < chance - EXCEPTIONAL_MARGIN then
        item.set_prop(made, "quality", EXCEPTIONAL_QUALITY)
        item.set_rarity(made, "uncommon")
        outcome = EXCEPTIONAL

        if marks then
            item.set_prop(made, "crafter_id", user)
            item.set_prop(made, "crafter_name", mobile.name(user))
            item.set_rarity(made, "rare")
            outcome = MARKED
        end
    end

    mobile.message_cliloc(user, outcome)

    if wear(user, tool, plain) then
        crafting.open(user, tool, craft_id, NOTICES[outcome])
    end
end

function crafting.make(user, tool, craft_id, group, index)
    if busy[user] and busy[user] > world.now() then
        mobile.message_cliloc(user, BUSY)

        return
    end

    local craft = craft_id and _G.craft.get(craft_id)
    local recipe = craft and craft.groups[group] and craft.groups[group].recipes[index]

    if not recipe then
        return
    end

    local kind = crafting.kind(user, craft_id)

    if not skilled(user, craft, recipe) then
        mobile.message_cliloc(user, NO_SKILL)
        crafting.open(user, tool, craft_id, NOTICES[NO_SKILL])

        return
    end

    -- The kind picked counts only for a recipe that takes its material, and asks for its skill.
    local material_id = crafting.material(craft)
    local material = MATERIALS[material_id]
    local picked = material and crafting.takes(recipe, material_id) and material.module.by_id(kind)

    if picked and points(user, craft.skill) < (picked[material.skill] or 0) then
        mobile.message_cliloc(user, material.cannot)
        crafting.open(user, tool, craft_id, NOTICES[material.cannot])

        return
    end

    local lacking = not_near(user, craft_id, craft.groups[group].name) or missing(user, recipe, kind)

    if lacking then
        mobile.message_cliloc(user, lacking)
        crafting.open(user, tool, craft_id, NOTICES[lacking])

        return
    end

    busy[user] = world.now() + GIVE_UP
    last[user] = last[user] or {}
    last[user][craft_id] = { group = group, index = index }
    crafting.uses(tool)
    mobile.play_sound(user, craft.sound)

    timer.after(STROKE, function()
        finish(user, tool, craft_id, craft, craft.groups[group].name, recipe, kind)
    end)
end

function crafting.make_last(user, tool, craft_id)
    local recipe = (last[user] or {})[craft_id]
    local data = recipe and _G.craft.get(craft_id)
    local group = data and data.groups[recipe.group]

    -- Nothing made yet with this craft, or a recipe the data no longer has.
    if not (group and group.recipes[recipe.index]) then
        mobile.message_cliloc(user, NOTHING_YET)
        crafting.open(user, tool, craft_id, NOTICES[NOTHING_YET])

        return
    end

    crafting.make(user, tool, craft_id, recipe.group, recipe.index)
end

return crafting
