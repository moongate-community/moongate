-- ==============================================================================
-- Moongate - scripts/gumps/craft_menu.lua
--
-- What it is for:
--   The script of the crafting gump (templates/gumps/craft_menu.xml), which
--   scripts/common/crafting.lua opens for a craft. On the left the groups of
--   the craft; on the right the recipes of the group picked, ten a page, each
--   with a button that makes it and one that shows its page (the item, what it
--   takes, the skills and the player's chance). Below, the wood picked and how
--   many boards of it the player carries, with a button to change it, a
--   button that makes the last recipe again, and the text of what happened
--   last. A button of a tool that is no longer in the
--   backpack does nothing.
--
-- Functions:
--   body(g, player, args)  fills the gump; args are those of the XML
-- ==============================================================================

local crafting = require("common.crafting")
local woods = require("common.woods")

craft_menu = {}

local PER_PAGE = 10
local ROW = 22
local FIRST_ROW = 30
local TEXT_HEIGHT = 20

local GROUPS_X = 0
local GROUPS_WIDTH = 140
local RECIPES_X = 175
local RECIPE_WIDTH = 270
local INFO_X = 470
local BOTTOM = 300

local TITLE_HUE = 1152
local PICKED_HUE = 1152

local function open(player, args, changes)
    local next_args = { craft = args.craft, tool = args.tool, page = "list" }

    for key, value in pairs(changes or {}) do
        next_args[key] = value
    end

    gump.open(player, "craft_menu", next_args)
end

local function label(g, x, y, width, text, hue)
    g:label_cropped{ x = x, y = y, width = width, height = TEXT_HEIGHT, hue = hue or 0, text = text }
end

-- A button that does something only while the tool is still in the player's backpack.
local function button(g, x, y, up, down, args, action)
    g:button{ x = x, y = y, up = up, down = down, on_click = function(who)
        if crafting.carries(who, args.tool) then
            action(who)
        end
    end }
end

local function wood_line(g, player, args)
    local kind = crafting.kind(player)
    local count = crafting.count(player, crafting.templates("wood", kind))

    button(g, 0, BOTTOM - 30, 4005, 4007, args, function(who)
        crafting.make_last(who, args.tool, args.craft)
    end)
    label(g, 35, BOTTOM - 30, 120, "Make last")

    label(g, 0, BOTTOM, 200, "Wood: " .. kind .. " (" .. count .. ")")
    button(g, 210, BOTTOM, 4005, 4007, args, function(who)
        open(who, args, { page = "woods" })
    end)
    label(g, 245, BOTTOM, 80, "Change")
    g:button{ x = 400, y = BOTTOM, up = 4017, down = 4019, on_click = function() end }
    label(g, 435, BOTTOM, 60, "Exit")

    if args.notice then
        label(g, 0, BOTTOM + 30, 490, args.notice, TITLE_HUE)
    end
end

local function groups_column(g, player, args, craft)
    for index, group in ipairs(craft.groups) do
        local y = FIRST_ROW + (index - 1) * ROW

        button(g, GROUPS_X, y, 4005, 4007, args, function(who)
            crafting.set_group(who, index)
            open(who, args)
        end)
        label(g, GROUPS_X + 35, y, GROUPS_WIDTH, group.name, index == crafting.group(player) and PICKED_HUE or 0)
    end
end

local function list_page(g, player, args, craft, group, group_index)
    g:pager{ previous = { x = RECIPES_X, y = BOTTOM - 30 }, next = { x = INFO_X, y = BOTTOM - 30 } }

    for index, recipe in ipairs(group.recipes) do
        local y = FIRST_ROW + g:paginate(index, PER_PAGE) * ROW

        button(g, RECIPES_X, y, 4005, 4007, args, function(who)
            crafting.make(who, args.tool, args.craft, group_index, index)
        end)
        label(g, RECIPES_X + 35, y, RECIPE_WIDTH, recipe.name)
        button(g, INFO_X, y, 4011, 4012, args, function(who)
            open(who, args, { page = "info", recipe = index })
        end)
    end
end

local function info_page(g, player, args, craft, group, group_index)
    local index = tonumber(args.recipe) or 1
    local recipe = group.recipes[index]

    if not recipe then
        return
    end

    local kind = crafting.kind(player)
    local y = FIRST_ROW

    g:item{ x = RECIPES_X, y = y, item = recipe.graphic }
    label(g, RECIPES_X + 60, y, RECIPE_WIDTH, recipe.name, TITLE_HUE)
    y = y + 50

    for _, resource in ipairs(recipe.resources) do
        local carried = crafting.count(player, crafting.templates(resource.resource, kind))

        label(g, RECIPES_X, y, RECIPE_WIDTH, resource.amount .. " " .. resource.resource .. " (" .. carried .. ")")
        y = y + ROW
    end

    label(g, RECIPES_X, y, RECIPE_WIDTH, string.format("%s %.1f - %.1f", craft.skill, recipe.skill_min, recipe.skill_max))
    y = y + ROW

    for _, other in ipairs(recipe.skills) do
        label(g, RECIPES_X, y, RECIPE_WIDTH, string.format("%s %.1f - %.1f", other.skill, other.min, other.max))
        y = y + ROW
    end

    label(g, RECIPES_X, y, RECIPE_WIDTH, string.format("Chance: %d%%", math.floor(crafting.chance(player, craft, recipe) * 100 + 0.5)))
    y = y + ROW * 2

    button(g, RECIPES_X, y, 4005, 4007, args, function(who)
        crafting.make(who, args.tool, args.craft, group_index, index)
    end)
    label(g, RECIPES_X + 35, y, 100, "Make")
    button(g, RECIPES_X + 150, y, 4014, 4016, args, function(who)
        open(who, args)
    end)
    label(g, RECIPES_X + 185, y, 100, "Back")
end

local function woods_page(g, player, args, craft)
    local all = { woods.plain }

    for _, kind in ipairs(woods.kinds) do
        all[#all + 1] = kind
    end

    for index, kind in ipairs(all) do
        local y = FIRST_ROW + (index - 1) * ROW
        local count = crafting.count(player, crafting.templates("wood", kind.id))

        button(g, RECIPES_X, y, 4005, 4007, args, function(who)
            crafting.set_kind(who, kind.id, craft.skill)
            open(who, args)
        end)
        label(g, RECIPES_X + 35, y, RECIPE_WIDTH, kind.name .. " (" .. count .. ")")
    end
end

function craft_menu.body(g, player, args)
    local craft = craft.get(args.craft or "")

    if not craft then
        return
    end

    local group_index = math.min(crafting.group(player), #craft.groups)
    local group = craft.groups[group_index]

    label(g, 0, 0, 490, string.upper(craft.name), TITLE_HUE)
    groups_column(g, player, args, craft)

    if group then
        if args.page == "info" then
            info_page(g, player, args, craft, group, group_index)
        elseif args.page == "woods" then
            woods_page(g, player, args, craft)
        else
            list_page(g, player, args, craft, group, group_index)
        end
    end

    wood_line(g, player, args)
end
