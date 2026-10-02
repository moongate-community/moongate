-- ==============================================================================
-- Moongate - scripts/gumps/tutorial_list.lua
--
-- What it is for:
--   The script of the dynamic list of the gump tutorial
--   (docs/gump-tutorial.md): fills the "rows" slot of
--   templates/gumps/tutorial_list.xml with one row per city, eight per page.
--
-- Functions:
--   rows(g, player, args)  fills the slot: g is a gump builder whose
--                          coordinates start at the slot; must not call wait()
-- ==============================================================================

tutorial_list = {}

local cities = {
    "Britain", "Buccaneer's Den", "Cove", "Jhelom", "Magincia", "Minoc", "Moonglow",
    "Nujel'm", "Ocllo", "Serpent's Hold", "Skara Brae", "Trinsic", "Vesper", "Yew"
}

function tutorial_list.rows(g, player, args)
    g:pager{ previous = { x = 0, y = 200 }, next = { x = 220, y = 200 } }

    for i, city in ipairs(cities) do
        local row = g:paginate(i, 8)

        g:text{ x = 30, y = row * 24, text = city }
        g:button{ x = 0, y = row * 24, up = 4005, down = 4007, on_click = function(player, response, args)
            log.info("Player {Player} picked {City}", player, city)
        end }
    end
end
