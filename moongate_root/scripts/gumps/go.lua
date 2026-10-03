-- ==============================================================================
-- Moongate - scripts/gumps/go.lua
--
-- What it is for:
--   The script of the gump of the named places (templates/gumps/go.xml), opened
--   by .go: it fills the "rows" slot with one level of data/locations.toml, the
--   categories first and then the places, twelve per page. A category opens
--   the gump one level down, a place takes the traveller there and keeps the
--   gump open. Staff only: anyone else sees an empty gump.
--
-- Functions:
--   rows(g, player, args)  fills the slot; args.path is the level to list,
--                          such as "Felucca/Dungeons", none for the maps
-- ==============================================================================

go = {}

local per_page = 12
local row_height = 22
local first_row = 50

local title_hue = 68
local category_hue = 1152

local function open(player, path)
    gump.open(player, "go", { path = path })
end

-- The level above: "Felucca/Dungeons" for "Felucca/Dungeons/Covetous", the maps for "Felucca".
local function above(path)
    return path:match("^(.*)/[^/]*$") or ""
end

function go.rows(g, player, args)
    if not world.is_staff(player) then
        return
    end

    -- A level that is no longer there, such as after the file changed: the maps.
    local node = locations.node(args.path or "") or locations.node("")

    g:text{ x = 0, y = 0, hue = title_hue, text = node.path == "" and "All maps" or node.path }

    if node.path ~= "" then
        g:button{ x = 0, y = 22, up = 4014, down = 4015, on_click = function(who)
            open(who, above(node.path))
        end }
        g:text{ x = 35, y = 22, text = "Back" }
    end

    g:pager{ previous = { x = 0, y = 330 }, next = { x = 290, y = 330 } }

    local index = 0

    for _, category in ipairs(node.categories) do
        index = index + 1
        local y = first_row + g:paginate(index, per_page) * row_height

        g:button{ x = 0, y = y, up = 4005, down = 4007, on_click = function(who)
            open(who, category.path)
        end }
        g:text{ x = 35, y = y, hue = category_hue, text = category.name }
    end

    for _, place in ipairs(node.locations) do
        index = index + 1
        local y = first_row + g:paginate(index, per_page) * row_height

        g:button{ x = 0, y = y, up = 4023, down = 4025, on_click = function(who)
            -- The rank may have gone while the gump was open.
            if not world.is_staff(who) then
                return
            end

            mobile.teleport(who, place.x, place.y, place.z, place.map)
            open(who, node.path)
        end }
        g:text{ x = 35, y = y, text = place.name }
    end
end
