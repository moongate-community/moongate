-- ==============================================================================
-- Moongate - scripts/gumps/stable_claim.lua
--
-- What it is for:
--   The script of the list of the pets in the stable (templates/gumps/
--   stable_claim.xml), opened by a stablemaster: it fills the "rows" slot with
--   one button and the name of the pet for each pet of stable.pets, eight a
--   page. A button takes that pet out of the stable: it comes back beside the
--   player. The player must still be within reach of the stablemaster, and the
--   list may have changed while the gump was open: then it is shown again.
--
-- Functions:
--   rows(g, player, args)  fills the slot; args.stablemaster is the stablemaster
-- ==============================================================================

stable_claim = {}

local per_page = 8
local row_height = 26
local text_height = 20
local row_width = 300

-- How far the player may be from the stablemaster, in cells.
local reach = 12

local TOO_FAR = 500446  -- That is too far away.
local HANDED = 1042559  -- Here you go... and good day to you!

local function in_reach(player, master)
    local at = mobile.location(player)
    local there = npc.location(master)

    return at ~= nil and there ~= nil and at.map == there.map
        and math.abs(at.x - there.x) <= reach and math.abs(at.y - there.y) <= reach
end

local function claim(player, args, place, template)
    if mobile.is_dead(player) then
        return
    end

    if not in_reach(player, args.stablemaster) then
        mobile.message_cliloc(player, TOO_FAR)

        return
    end

    local result = stable.claim(player, place, template)

    if result == StableResultType.Ok then
        npc.say_cliloc(args.stablemaster, HANDED)
    elseif result == StableResultType.BadIndex then
        -- The list changed since it was shown: shown again as it is.
        gump.open(player, "stable_claim", args)
    end
end

function stable_claim.rows(g, player, args)
    g:pager{ previous = { x = 0, y = 230 }, next = { x = 300, y = 230 } }

    for place, pet in ipairs(stable.pets(player) or {}) do
        local y = g:paginate(place, per_page) * row_height

        g:button{ x = 0, y = y, up = 4005, down = 4007, on_click = function(who)
            claim(who, args, place, pet.template)
        end }
        g:label_cropped{ x = 35, y = y, width = row_width, height = text_height, text = pet.name }
    end
end
