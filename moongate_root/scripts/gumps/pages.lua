-- ==============================================================================
-- Moongate - scripts/gumps/pages.lua
--
-- What it is for:
--   The script of the queue of the requests for the game masters (templates/gumps/
--   pages.xml), opened by .pages: it fills the "rows" slot with the open and taken
--   requests of help.pages, the oldest first, ten a page. A row opens pages_detail
--   on that request. Staff only: anyone else sees an empty gump.
--
-- Functions:
--   rows(g, player, args)  fills the slot
-- ==============================================================================

pages = {}

local help_pages = require("common.help_pages")

local per_page = 10
local row_height = 28
local text_height = 20
local row_width = 380

function pages.rows(g, player, args)
    if not world.is_staff(player) then
        return
    end

    g:pager{ previous = { x = 0, y = 300 }, next = { x = 390, y = 300 } }

    for index, page in ipairs(help.pages()) do
        local y = g:paginate(index, per_page) * row_height

        g:button{ x = 0, y = y, up = 4005, down = 4007, on_click = function(who)
            -- The rank may have gone while the gump was open.
            if not world.is_staff(who) then
                return
            end

            gump.open(who, "pages_detail", help_pages.detail_args(page))
        end }
        g:label_cropped{ x = 35, y = y, width = row_width, height = text_height, text = help_pages.row_text(page) }
    end
end
