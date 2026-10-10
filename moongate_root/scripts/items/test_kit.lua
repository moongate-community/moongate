-- ==============================================================================
-- Moongate - scripts/items/test_kit.lua
--
-- What it is for:
--   The item script of the test bags for the staff, with
--   script_id = "test_kit" (templates/items/test_kits.toml): ".add
--   test_kit_alchemy" gives a bag that fills, the first time it is opened,
--   with what is needed to try a craft, a potion or the spells of the first
--   circle: the tool, the materials and the items to use, or a full spellbook,
--   the reagents and some scrolls. Then it is a bag like any other. The skill
--   to use them is set apart, as ".set skill alchemy 100" or ".set skill magery
--   100".
--
-- Functions:
--   test_kit.on_use(serial, user)   fills the bag once; the bag then opens
-- ==============================================================================

test_kit = {}

local FILLED_PROP = "kit.filled"

-- What each bag holds: a template and how many.
local KITS = {
    test_kit_alchemy = {
        { "mortarandpestle", 1 }, { "0x0f0e_empty_bottle", 30 }, { "0x0f7a_black_pearl", 20 },
        { "0x0f7b_blood_moss", 20 }, { "0x0f84_garlic", 20 }, { "0x0f85_ginseng", 20 }, { "0x0f86_mandrake_root", 20 },
        { "0x0f88_nightshade", 20 }, { "0x0f8c_sulfurous_ash", 20 }, { "0x0f8d_spider_silk", 20 },
    },
    test_kit_potions = {
        { "0x0f0c_yellow_potion", 3 }, { "0x0f0c_b_yellow_potion", 3 }, { "0x0f0c_c_yellow_potion", 3 },
        { "0x0f0b_red_potion", 3 }, { "0x0f0b_b_red_potion", 3 }, { "0x0f09_white_potion", 3 },
        { "0x0f09_b_white_potion", 3 }, { "0x0f08_blue_potion", 3 }, { "0x0f08_b_blue_potion", 3 },
        { "0x0f06_black_potion", 3 }, { "0x0f0a_green_potion", 3 }, { "0x0f0a_b_green_potion", 3 },
        { "0x0f0a_c_green_potion", 3 }, { "0x0f0a_d_green_potion", 3 }, { "0x0f07_orange_potion", 3 },
        { "0x0f07_b_orange_potion", 3 }, { "0x0f07_c_orange_potion", 3 },
    },
    test_kit_explosion = {
        { "lesserexplosionpotion", 5 }, { "explosionpotion", 5 }, { "greaterexplosionpotion", 5 },
    },
    test_kit_cartography = {
        { "mapmakerspen", 1 }, { "0x14ec_blank_map", 10 }, { "britainmap", 1 }, { "smallworldmap", 1 },
    },
    test_kit_tailoring = {
        { "0x0f9d_sewing_kit", 1 }, { "0x1766_cut_cloth", 100 }, { "0x1081_cut_up_leather", 50 },
    },
    test_kit_tinkering = {
        { "0x1ebc_tinker's_tools", 1 }, { "0x1bf2_iron_ingot", 50 }, { "ingot_copper", 20 }, { "0x0f26_diamond", 5 },
        { "0x1422_beeswax", 10 },
    },
    test_kit_fletching = {
        { "0x1022_fletcher's_tools", 1 }, { "0x1bd7_board", 50 }, { "0x1bd1_feather", 50 },
    },
    test_kit_magery = {
        { "spellbook_full", 1 }, { "0x0f7a_black_pearl", 20 }, { "0x0f7b_blood_moss", 20 }, { "0x0f84_garlic", 20 },
        { "0x0f85_ginseng", 20 }, { "0x0f86_mandrake_root", 20 }, { "0x0f88_nightshade", 20 },
        { "0x0f8c_sulfurous_ash", 20 }, { "0x0f8d_spider_silk", 20 }, { "0x1f2e_clumsy_scroll", 3 },
        { "0x1f2f_create_food_scroll", 3 }, { "0x1f30_feeblemind_scroll", 3 }, { "0x1f31_heal_scroll", 3 },
        { "0x1f32_magic_arrow_scroll", 3 }, { "0x1f33_night_sight_scroll", 3 }, { "0x1f34_weaken_scroll", 3 },
    },
    test_kit_cooking = {
        { "0x097f_skillet", 1 }, { "0x1039_sack_of_flour", 5 }, { "0x0ff8_pitcher_of_water", 5 },
        { "0x097a_raw_fish_steak", 5 }, { "0x09b5_eggs", 5 },
    },
}

local function put(bag, template, amount, here)
    if item.is_stackable(template) then
        local made = item.create(template, here.map, here.x, here.y, here.z, amount)

        if made then
            item.move_into(made, bag)
        end

        return
    end

    for _ = 1, amount do
        local made = item.create(template, here.map, here.x, here.y, here.z)

        if made then
            item.move_into(made, bag)
        end
    end
end

function test_kit.on_use(serial, user)
    local contents = KITS[item.template(serial) or ""]

    if not contents or item.get_prop(serial, FILLED_PROP) then
        return
    end

    local here = mobile.location(user)

    if not here then
        return
    end

    item.set_prop(serial, FILLED_PROP, true)

    for _, entry in ipairs(contents) do
        put(serial, entry[1], entry[2], here)
    end
end
