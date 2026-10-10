-- ==============================================================================
-- Moongate - scripts/gumps/polymorph_forms.lua
--
-- What it is for:
--   The script of the list of forms of the spell Polymorph (templates/gumps/
--   polymorph_forms.xml): a button keeps the body it stands for in the props of the
--   player (magic.polymorph_body, until magic.polymorph_until, a quarter of a
--   minute) and casts the spell again, from the book or, when it was a scroll
--   that opened the list, from a scroll of the same spell in the backpack; the
--   second cast, which asks for no list, changes the body. A player that has no
--   book or scroll left, or is not able to cast, is told by the cast itself.
--
-- Functions:
--   one function for each form, named by the button: chicken, dog, wolf,
--   panther, gorilla, black_bear, grizzly_bear, polar_bear, human_male, slime,
--   orc, lizardman, gargoyle, ogre, troll, ettin, daemon, human_female
-- ==============================================================================

polymorph_forms = {}

-- The bodies, by the name of the button.
local FORMS = {
    chicken = 0xd0,
    dog = 0xd9,
    wolf = 0xe1,
    panther = 0xd6,
    gorilla = 0x1d,
    black_bear = 0xd3,
    grizzly_bear = 0xd4,
    polar_bear = 0xd5,
    human_male = 0x190,
    slime = 0x33,
    orc = 0x11,
    lizardman = 0x21,
    gargoyle = 0x4,
    ogre = 0x1,
    troll = 0x36,
    ettin = 0x2,
    daemon = 0x9,
    human_female = 0x191,
}

-- How long a pick waits for its cast, in seconds.
local PICK_SECONDS = 15

local function again(player)
    local from_scroll = mobile.get_prop(player, "magic.polymorph_scroll") == true
    local info = spell.info("polymorph")

    if from_scroll and info then
        local pack = mobile.backpack(player)
        local scrolls = pack and item.find(pack, info.scroll) or {}

        if scrolls[1] then
            spell.cast_scroll(player, scrolls[1])

            return
        end
    end

    spell.cast(player, "polymorph")
end

for key, body in pairs(FORMS) do
    polymorph_forms[key] = function(player, response, args)
        if mobile.is_dead(player) then
            return
        end

        mobile.set_prop(player, "magic.polymorph_body", body)
        mobile.set_prop(player, "magic.polymorph_until", world.now() + PICK_SECONDS)
        again(player)
    end
end
