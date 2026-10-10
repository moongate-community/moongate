-- ==============================================================================
-- Moongate - scripts/items/test_kit.lua
--
-- What it is for:
--   The item script of the test bags for the staff, with
--   script_id = "test_kit" (templates/items/test_kits.toml): ".add
--   test_kit_alchemy" gives a bag that fills, the first time it is opened,
--   with what is needed to try a craft (inscription takes a pen and ink, blank
--   scrolls, the reagents and a full spellbook; set the skill with ".set skill
--   inscription 100" and magery for the mana), a potion or the spells of the first
--   to eighth circles: the tool, the materials and the items to use, or a full
--   spellbook, the reagents, some scrolls and four recall runes (marked with
--   ".mark_rune"). Then it is a bag like any other. The skill
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
        { "0x1f2d_reactive_armor_scroll", 3 }, { "0x1f35_agility_scroll", 3 }, { "0x1f36_cunning_scroll", 3 },
        { "0x1f37_cure_scroll", 3 }, { "0x1f38_harm_scroll", 3 }, { "0x1f3b_protection_scroll", 3 },
        { "0x1f3c_strength_scroll", 3 }, { "0x1f3d_bless_scroll", 3 }, { "0x1f3e_fireball_scroll", 3 },
        { "0x1f40_poison_scroll", 3 }, { "0x1f41_telekinisis_scroll", 3 }, { "0x1f42_teleport_scroll", 3 },
        { "0x1f44_wall_of_stone_scroll", 3 }, { "0x1f45_archcure_scroll", 3 }, { "0x1f46_archprotection_scroll", 3 },
        { "0x1f47_curse_scroll", 3 }, { "0x1f48_fire_field_scroll", 3 }, { "0x1f49_greater_heal_scroll", 3 },
        { "0x1f4a_lightning_scroll", 3 }, { "0x1f4b_mana_drain_scroll", 3 }, { "0x1f4c_recall_scroll", 3 },
        { "0x1f4d_blade_spirits_scroll", 3 }, { "0x1f4e_dispel_field_scroll", 3 }, { "0x1f4f_incognito_scroll", 3 },
        { "0x1f50_magic_reflection_scroll", 3 }, { "0x1f51_mind_blast_scroll", 3 }, { "0x1f52_paralyze_scroll", 3 },
        { "0x1f53_poison_field_scroll", 3 }, { "0x1f54_summon_creature_scroll", 3 }, { "0x1f55_dispel_scroll", 3 },
        { "0x1f56_energy_bolt_scroll", 3 }, { "0x1f57_explosion_scroll", 3 }, { "0x1f58_invisibility_scroll", 3 },
        { "0x1f59_mark_scroll", 3 }, { "0x1f5a_mass_curse_scroll", 3 }, { "0x1f5b_paralyze_field_scroll", 3 },
        { "0x1f5c_reveal_scroll", 3 }, { "0x1f5d_chain_lightning_scroll", 3 }, { "0x1f5e_energy_field_scroll", 3 },
        { "0x1f5f_flamestrike_scroll", 3 }, { "0x1f60_gate_travel_scroll", 3 }, { "0x1f61_mana_vampire_scroll", 3 },
        { "0x1f62_mass_dispel_scroll", 3 }, { "0x1f63_meteor_storm_scroll", 3 }, { "0x1f64_polymorph_scroll", 3 },
        { "0x1f65_earthquake_scroll", 3 }, { "0x1f66_energy_vortex_scroll", 3 }, { "0x1f67_resurrection_scroll", 3 },
        { "0x1f68_summon_air_elemental_scroll", 3 }, { "0x1f69_summon_daemon_scroll", 3 },
        { "0x1f6a_summon_earth_elemental_scroll", 3 }, { "0x1f6b_summon_fire_elemental_scroll", 3 },
        { "0x1f6c_summon_water_elemental_scroll", 3 },
        { "recall_rune", 4 },
    },
    test_kit_inscription = {
        { "0x0fc0_pen_and_ink", 1 }, { "0x0e34_a_blank_scroll", 100 }, { "spellbook_full", 1 },
        { "0x0f7a_black_pearl", 50 }, { "0x0f7b_blood_moss", 50 }, { "0x0f84_garlic", 50 }, { "0x0f85_ginseng", 50 },
        { "0x0f86_mandrake_root", 50 }, { "0x0f88_nightshade", 50 }, { "0x0f8c_sulfurous_ash", 50 },
        { "0x0f8d_spider_silk", 50 },
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
