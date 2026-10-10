"""The item and loot passes of ``uox``, ported from the C# ``UoxItemConverterCommandTests``: each test writes ``.dfn`` blocks, runs the
command and reads the item and loot files back."""

from __future__ import annotations

import pytest

from conftest import read_toml


def test_a_block_with_its_own_fields_converts_every_mapped_field(uox_workspace):
    uox_workspace.write_source("items.dfn", "[base_torch]\n{\nname=torch\nid=0x0f6b\nmovable=1\ncolor=0x0010\nweightmax=40050\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["id"] == "base_torch"
    assert item["item_id"] == 0x0F6B
    assert item["name"] == "torch"
    assert item["movable"] is True
    assert item["hue"] == 0x0010
    # UOX3 counts a container's limit in hundredths of a stone too: 400.5 stones, kept whole and rounded up.
    assert item["max_weight"] == 401
    assert "base_id" not in item


def test_what_uox3_calls_food_gets_the_food_script(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[base_food]\n{\ntype=14\nweight=100\n}\n"
        "[0x09d0]\n{\nget=base_food\nname=apple\nid=0x09d0\n}\n"
        "[0x0a1e]\n{\nget=base_food\nname=bowl of flour\nid=0x0a1e\n}\n"
        "[base_magic_fish]\n{\nget=base_food\nid=0x0dd6\n}\n"
        "[0x1f9e]\n{\nname=pitcher of water\nid=0x1f9e\ntype=105\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # UOX3's item type 14 is food, on the block itself or the one it gets its fields from; a drink (type 105) has its own script.
    # Nor is what UOX3 files under food and nobody eats: a bowl of flour, the magic fish.
    items = uox_workspace.items()
    by_name = {item.get("name"): item for item in items.values()}
    assert "script_id" not in by_name["bowl of flour"]
    assert "script_id" not in items["base_magic_fish"]
    assert by_name["apple"]["script_id"] == "food"
    assert by_name["pitcher of water"]["script_id"] == "drink"


def test_what_uox3_calls_an_axe_gets_the_axe_script(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[base_hatchet]\n{\ntype=216\nid=0x0f43\n}\n"
        "[0x0f44]\n{\nget=base_hatchet\nname=hatchet\nid=0x0f44\n}\n"
        "[0x13af]\n{\nname=war axe\nid=0x13af\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # UOX3's item type 216 is what chops a tree: scripts/items/axe.lua. A war axe is no such thing.
    items = uox_workspace.items()
    by_name = {item.get("name"): item for item in items.values()}
    # The hatchet takes it from its base, as every field it does not set itself.
    assert items["base_hatchet"]["script_id"] == "axe"
    assert by_name["hatchet"]["base_id"] == "base_hatchet"
    assert "script_id" not in by_name["war axe"]


def test_the_blades_get_the_blade_script_by_their_graphic_but_what_is_no_weapon(uox_workspace):
    uox_workspace.write_source(
        "blades.dfn",
        "[0x0f51]\n{\nname=dagger\nid=0x0f51\n}\n"
        "[0x2306]\n{\nname=flower garland\nid=0x2306\n}\n"
        "[0x0f49]\n{\nname=axe\nid=0x0f49\ntype=216\n}\n",
    )
    uox_workspace.write_scripts("jse_fileassociations.scp", "[SCRIPT_LIST]\n{\n5009=item/sword.js\n}\n")
    uox_workspace.write_scripts("jse_objectassociations.scp", "[ENVOKE]\n{\n0x0f51=5009\n0x2306=5009\n}\n")

    assert uox_workspace.run(scripts=True) == 0, uox_workspace.combined

    # UOX3 binds its blade script by graphic, and to a garland by mistake: only what is a weapon takes scripts/items/blade.lua.
    by_name = {item.get("name"): item for item in uox_workspace.items("blades.toml").values()}
    assert by_name["dagger"]["script_id"] == "blade"
    assert "script_id" not in by_name["flower garland"]
    assert by_name["axe"]["script_id"] == "axe"


def test_the_mining_tools_and_the_piles_of_ore_get_their_scripts(uox_workspace):
    uox_workspace.write_source(
        "mining.dfn",
        "[0x0e85]\n{\nname=pickaxe\nid=0x0e85\nscript=4050\n}\n"
        "[0x0f39]\n{\nname=a shovel\nid=0x0f39\nscript=4050\n}\n"
        "[0x19b9]\n{\nname=iron ore\nid=0x19b9\n}\n"
        "[shadow_iron_ore]\n{\nname=shadow iron ore\nid=0x19b9\ncolor=0x0966\n}\n"
        "[0x1bf2]\n{\nname=iron ingot\nid=0x1bf2\n}\n",
    )
    uox_workspace.write_scripts("jse_fileassociations.scp", "[SCRIPT_LIST]\n{\n4050=skill/mining.js\n}\n")
    uox_workspace.write_scripts("jse_objectassociations.scp", "[ENVOKE]\n{\n}\n")

    assert uox_workspace.run(scripts=True) == 0, uox_workspace.combined

    # UOX3 gives its mining script to the tools in their block, and types the piles of ore by their graphic.
    by_name = {item.get("name"): item for item in uox_workspace.items("mining.toml").values()}
    assert by_name["pickaxe"]["script_id"] == "pickaxe"
    assert by_name["a shovel"]["script_id"] == "pickaxe"
    assert by_name["iron ore"]["script_id"] == "ore"
    assert "script_id" not in by_name["shadow iron ore"]
    assert "script_id" not in by_name["iron ingot"]


def test_what_uox3_calls_a_drink_gets_the_drink_script_but_the_jar_of_honey(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[base_drink]\n{\ntype=105\nscript=2100\n}\n"
        "[0x1f9e]\n{\nget=base_drink\nname=pitcher of water\nid=0x1f9e\n}\n"
        "[0x09ec]\n{\nget=base_drink\nname=jar of honey\nid=0x09ec\n}\n"
        "[0x09d0]\n{\nname=apple\nid=0x09d0\ntype=14\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert by_name["pitcher of water"]["script_id"] == "drink"
    assert "script_id" not in by_name["jar of honey"]
    assert by_name["apple"]["script_id"] == "food"


def test_dyes_the_dye_tub_and_what_uox3_calls_dyeable_get_their_scripts_and_the_flag(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[base_clothing]\n{\nid=0x1f03\ndyeable=1\n}\n"
        "[0x1517]\n{\nget=base_clothing\nname=shirt\nid=0x1517\n}\n"
        "[0x204e]\n{\nget=base_clothing\nname=death shroud\nid=0x204e\ndyeable=0\n}\n"
        "[0x2b68]\n{\nname=snowy cloak\nid=0x2b68\ndye=1\n}\n"
        "[0x0fa9]\n{\nname=dyes\nid=0x0fa9\ntype=208\n}\n"
        "[0x0fab]\n{\nname=dying tub\nid=0x0fab\n}\n"
        "[0x0f5e]\n{\nname=broadsword\nid=0x0f5e\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    by_name = {item["name"]: item for item in items.values() if item["id"] != "base_clothing"}
    assert items["base_clothing"]["dyeable"] is True
    # The shirt has it from its base, written once.
    assert "dyeable" not in by_name["shirt"]
    assert by_name["death shroud"]["dyeable"] is False
    assert by_name["snowy cloak"]["dyeable"] is True
    assert "dyeable" not in by_name["broadsword"]
    # UOX3 types the dyes in the block and the tub by its graphic, in itemtypes.dfn.
    assert by_name["dyes"]["script_id"] == "dyes"
    assert by_name["dying tub"]["script_id"] == "dye_tub"
    assert "script_id" not in by_name["broadsword"]


def test_the_base_fields_are_converted(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[coin]\n{\nname=gold coin\nid=0x0eed\nweight=2\namount=5\npileable=1\nlayer=21\nvalue=60 30\ndecay=1\nnewbie\n"
        "custominttag=Level 7\ncustomstringtag=Owner Mario Rossi\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["weight"] == 0.02
    assert item["amount"] == 5
    assert item["stackable"] is True
    assert item["layer"] == "backpack"
    assert (item["buy_price"], item["sell_price"]) == (60, 30)
    assert item["decays"] is True
    assert item["loot_type"] == "newbied"
    assert item["tags"]["Level"] == "7"
    assert item["tags"]["Owner"] == "Mario Rossi"


@pytest.mark.parametrize(("value_line", "buy", "sell"), [("value=40", 40, 40), ("", None, None)])
def test_one_value_or_none_sets_both_prices_or_neither(uox_workspace, value_line, buy, sell):
    uox_workspace.write_source("items.dfn", f"[lamp]\n{{\nid=0x0a22\n{value_line}\n}}\n")

    assert uox_workspace.run() == 0

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert (item.get("buy_price"), item.get("sell_price")) == (buy, sell)


@pytest.mark.parametrize(
    ("line", "movable"),
    [("movable=1", True), ("movable=3", True), ("movable=2", False), ("movable=0", None), ("", None), ("decay=0", None)],
)
def test_movable_follows_uox3_and_unset_means_tiledata(uox_workspace, line, movable):
    uox_workspace.write_source("items.dfn", f"[lamp]\n{{\nid=0x0a22\n{line}\n}}\n")

    assert uox_workspace.run() == 0

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]

    if line.startswith("decay"):
        assert item["decays"] is False
    else:
        assert item.get("movable") == movable


@pytest.mark.parametrize(
    ("visible_line", "expected"),
    [("", None), ("visible=0", "regular"), ("visible=1", "game_master"), ("visible=2", "game_master"), ("visible=3", "game_master")],
)
def test_the_visible_field_maps_hidden_items_to_game_master_visibility(uox_workspace, visible_line, expected):
    uox_workspace.write_source("items.dfn", f"[orcspawn]\n{{\nname=Orc Spawner\nid=0x1f13\n{visible_line}\n}}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item.get("visibility") == expected


def test_a_block_with_a_single_get_target_resolves_base_id_against_the_converted_parent(uox_workspace):
    uox_workspace.write_source(
        "items.dfn", "[base_torch]\n{\nname=torch\nid=0x0f6b\n}\n\n[wall_torch]\n{\nget=base_torch\nname=wall_torch\nid=0x0393\n}\n"
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    assert uox_workspace.items()["wall_torch"]["base_id"] == "base_torch"


def test_a_get_target_with_no_id_of_its_own_is_flattened_into_the_child(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[base_item]\n{\nid=0x0000\n}\n\n"
        "[base_metal]\n{\nget=base_item\nweight=50\ncustominttag=Metal 1\n}\n\n"
        "[base_coin]\n{\nget=base_metal\nweight=2\npileable=1\ndecay=1\ncustominttag=Coin 1\n}\n\n"
        "[0x0eed]\n{\nget=base_coin\nname=gold coin\nid=0x0eed\ndecay=0\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    # base_metal and base_coin have one parent and fields of their own, so they are templates too, but the coin still gets their fields
    # inlined and inherits from the first ancestor with an id= of its own.
    assert items["base_coin"]["base_id"] == "base_item"
    coin = items["0x0eed_gold_coin"]
    assert coin["weight"] == 0.02
    assert coin["stackable"] is True
    assert coin["decays"] is False
    assert coin["base_id"] == "base_item"
    assert (coin["tags"]["Metal"], coin["tags"]["Coin"]) == ("1", "1")


def test_an_inherited_name_is_the_template_name_but_not_part_of_the_id(uox_workspace):
    uox_workspace.write_source("items.dfn", "[base_sleeves]\n{\nname=studded sleeves\n}\n\n[0x13dc]\n{\nget=base_sleeves\nid=0x13dc\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["id"] == "0x13dc"
    assert item["name"] == "studded sleeves"


def test_a_get_cycle_between_blocks_with_no_id_stops_without_looping(uox_workspace):
    uox_workspace.write_source(
        "items.dfn", "[base_a]\n{\nget=base_b\nweight=1\n}\n\n[base_b]\n{\nget=base_a\nweight=2\n}\n\n[lamp]\n{\nget=base_a\nid=0x0a22\n}\n"
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["weight"] == 0.01


def test_a_block_that_gets_itself_leaves_base_id_unset(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x27c2]\n{\nget=0x27c2\nid=0x27c2\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert "base_id" not in item


def test_a_get_target_that_never_converted_leaves_base_id_unset(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x1440]\n{\ngett2a=0x1440_t2a\n}\n\n[0x1441]\n{\nget=0x1440\nid=0x1441\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["id"] == "0x1441"
    assert "base_id" not in item


def test_a_block_with_one_parent_and_no_id_of_its_own_becomes_a_template_inheriting_the_graphic(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[0x0df1]\n{\nid=0x0df1\nname=magic staff\n}\n\n"
        "[glacialstaff]\n{\nget=0x0df1\nname=glacial staff\ncolor=0x0480\n}\n\n"
        "[frost_staff]\n{\nget=glacialstaff\nname=frost staff\n}\n\n"
        "[plain_staff]\n{\ngetlbr=0x0df1\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    glacial = items["glacialstaff"]
    assert (glacial["base_id"], glacial["item_id"], glacial["name"], glacial["hue"]) == ("0x0df1_magic_staff", 0, "glacial staff", 0x0480)
    assert (items["frost_staff"]["base_id"], items["frost_staff"]["name"]) == ("0x0df1_magic_staff", "frost staff")
    assert items["plain_staff"]["base_id"] == "0x0df1_magic_staff"


def test_a_loot_entry_naming_a_block_with_no_id_of_its_own_resolves(uox_workspace):
    uox_workspace.write_source(
        "items.dfn", "[0x0df1]\n{\nid=0x0df1\n}\n\n[glacialstaff]\n{\nget=0x0df1\nname=glacial staff\n}\n\n[LOOTLIST staffs]\n{\nglacialstaff\n}\n"
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    (entry,) = uox_workspace.loot("staffs")["entries"]
    assert entry["item_id"] == "glacialstaff"


def test_a_block_with_multiple_get_targets_and_no_id_of_its_own_is_skipped(uox_workspace):
    uox_workspace.write_source("items.dfn", "[base_torch]\n{\nid=0x0f6b\n}\n\n[torch_alias]\n{\nget=base_torch 0x0393\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["id"] == "base_torch"


def test_a_name_on_a_bare_hex_header_prefixes_the_name_with_the_header(uox_workspace):
    # The header, not the name, is what guarantees uniqueness: UOX3 reuses the same name= across many variants of the same conceptual item.
    uox_workspace.write_source("items.dfn", "[0x1441]\n{\nname=cutlass_ns\nid=0x1441\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["id"] == "0x1441_cutlass_ns"


def test_a_name_with_spaces_on_a_bare_hex_header_produces_a_snake_case_id(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x1f9b]\n{\nname=pitcher of wine\nid=0x1f9b\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = read_toml(uox_workspace.destination / "items.toml")["item"]
    assert item["id"] == "0x1f9b_pitcher_of_wine"


def test_the_same_name_on_different_bare_hex_headers_produces_distinct_ids(uox_workspace):
    uox_workspace.write_source("doors.dfn", "[0x0334]\n{\nname=#\nid=0x0334\n}\n\n[0x0336]\n{\nname=#\nid=0x0336\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    assert len(set(uox_workspace.items("doors.toml"))) == 2


def test_a_directory_of_source_files_mirrors_their_relative_paths_under_destination(uox_workspace):
    uox_workspace.write_source("gear/weapons/swords.dfn", "[base_katana]\n{\nid=0x13fe\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    assert (uox_workspace.destination / "gear" / "weapons" / "swords.toml").is_file()


def test_a_loot_list_block_converts_weighted_entries_against_items_in_the_same_file(uox_workspace):
    uox_workspace.write_source("loot.dfn", "[0x0f0f]\n{\nid=0x0f0f\n}\n\n[LOOTLIST eartheleLoot]\n{\n40|blank\n10|0x0f0f\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    # Each loot table writes to its own file, named after its own id, not the source .dfn's. Real LOOTLIST names are camelCase.
    loot = uox_workspace.loot("earthele_loot")
    assert loot["id"] == "earthele_loot"
    assert len(loot["entries"]) == 2
    blank = next(entry for entry in loot["entries"] if entry["weight"] == 40)
    assert "item_id" not in blank and "loot_template_id" not in blank
    entry = next(entry for entry in loot["entries"] if entry["weight"] == 10)
    assert entry["item_id"] == "0x0f0f"
    assert "loot_template_id" not in entry


def test_a_loot_entry_for_an_item_with_a_name_carries_that_name_as_a_comment(uox_workspace):
    uox_workspace.write_source(
        "loot.dfn",
        "[raw_iron_ore]\n{\nid=0x19b7\nname=iron ore\n}\n\n[0x0f81]\n{\nid=0x0f81\n}\n\n[LOOTLIST eartheleLoot]\n{\n10|raw_iron_ore\n10|0x0f81\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    entries = uox_workspace.loot("earthele_loot")["entries"]
    assert next(entry for entry in entries if entry["item_id"] == "raw_iron_ore")["comment"] == "iron ore"
    assert "comment" not in next(entry for entry in entries if entry["item_id"] == "0x0f81")


def test_a_loot_entry_with_no_weight_prefix_defaults_to_weight_one(uox_workspace):
    uox_workspace.write_source("loot.dfn", "[0x0f0f]\n{\nid=0x0f0f\n}\n\n[LOOTLIST unweighted]\n{\n0x0f0f\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (entry,) = uox_workspace.loot("unweighted")["entries"]
    assert entry["weight"] == 1


def test_a_nested_loot_list_reference_resolves_loot_template_id_and_amount(uox_workspace):
    uox_workspace.write_source(
        "loot.dfn", "[LOOTLIST randomgems]\n{\n10|blank\n}\n\n[LOOTLIST eartheleLoot]\n{\n10|LOOTLIST=randomgems,2\n}\n"
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    (entry,) = uox_workspace.loot("earthele_loot")["entries"]
    assert entry["loot_template_id"] == "randomgems"
    assert "item_id" not in entry
    assert entry["amount"] == 2


def test_multiple_loot_list_blocks_in_the_same_file_each_write_their_own_file(uox_workspace):
    uox_workspace.write_source("loot.dfn", "[LOOTLIST randomgems]\n{\n10|blank\n}\n\n[LOOTLIST eartheleLoot]\n{\n10|blank\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    assert (uox_workspace.loot_destination / "randomgems.toml").is_file()
    assert (uox_workspace.loot_destination / "earthele_loot.toml").is_file()
    assert not (uox_workspace.loot_destination / "loot.toml").exists()


def test_a_loot_entry_pointing_at_nothing_resolvable_is_skipped(uox_workspace):
    uox_workspace.write_source("loot.dfn", "[LOOTLIST eartheleLoot]\n{\n10|LOOTLIST=nonexistent\n10|0x9999\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    assert "2 loot entry/entries" in uox_workspace.combined
    assert uox_workspace.loot("earthele_loot")["entries"] == []


def test_a_loot_entry_with_a_min_max_amount_parses_a_range(uox_workspace):
    uox_workspace.write_source("loot.dfn", "[0x0f0f]\n{\nid=0x0f0f\n}\n\n[LOOTLIST eartheleLoot]\n{\n10|0x0f0f,1 3\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (entry,) = uox_workspace.loot("earthele_loot")["entries"]
    assert entry["amount"] == "1-3"


def test_a_loot_entry_with_a_reversed_amount_range_is_a_bad_source(uox_workspace):
    uox_workspace.write_source("loot.dfn", "[0x0f0f]\n{\nid=0x0f0f\n}\n\n[LOOTLIST eartheleLoot]\n{\n10|0x0f0f,3 1\n}\n")

    assert uox_workspace.run() == 2
    assert "not a range" in uox_workspace.error.getvalue()


def test_a_loot_entry_referencing_an_item_defined_in_a_file_scanned_later_still_resolves(uox_workspace):
    # File names deliberately sort the referencing loot block before the item that defines its target: the ordering that broke id resolution
    # before every block's id was precomputed up front.
    uox_workspace.write_source("aaa_lootlist.dfn", "[LOOTLIST eartheleLoot]\n{\n10|0x0f0f\n}\n")
    uox_workspace.write_source("zzz_items.dfn", "[0x0f0f]\n{\nid=0x0f0f\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    (entry,) = uox_workspace.loot("earthele_loot")["entries"]
    assert entry["item_id"] == "0x0f0f"


def test_without_a_loot_destination_loot_list_blocks_stay_unconverted(uox_workspace):
    uox_workspace.write_source("loot.dfn", "[0x0f0f]\n{\nid=0x0f0f\n}\n\n[LOOTLIST eartheleLoot]\n{\n10|0x0f0f\n}\n")

    assert uox_workspace.run(loot=False) == 0, uox_workspace.combined

    assert len(read_toml(uox_workspace.destination / "loot.toml")["item"]) == 1
    assert not uox_workspace.loot_destination.exists()


def test_a_successful_conversion_verifies_the_output_read_back_from_disk(uox_workspace):
    uox_workspace.write_source("items.dfn", "[base_torch]\n{\nid=0x0f6b\n}\n\n[LOOTLIST eartheleLoot]\n{\n10|base_torch\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    assert "Verified 1 item(s) and 1 loot table(s) read back from disk" in uox_workspace.combined


def test_two_headers_colliding_only_after_snake_case_fail_verification_with_a_non_zero_exit_code(uox_workspace):
    # "Base-Item" and "base_item" are two distinct headers, but snake case collapses both to the same id, which only a real read back of what
    # was written can catch.
    uox_workspace.write_source("items.dfn", "[Base-Item]\n{\nid=0x0f6b\n}\n\n[base_item]\n{\nid=0x0f6c\n}\n")

    assert uox_workspace.run() != 0
    assert "Verification failed: item 'base_item' is defined more than once" in uox_workspace.combined


def test_an_id_list_or_a_typoed_id_keeps_the_first_graphic(uox_workspace):
    # UOX3 picks one id of a list at random (items.cpp); real data also has id=0x0x04FC and id=0x15b6].
    uox_workspace.write_source(
        "items.dfn",
        "[base_item]\n{\nid=0x0000\n}\n[cotton]\n{\nget=base_item\nid=0x0c4f 0x0c50\n}\n[0x04fc]\n{\nid=0x0x04FC\n}\n[0x15b6]\n{\nid=0x15b6]\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    assert (items["cotton"]["item_id"], items["cotton"]["base_id"]) == (0x0C4F, "base_item")
    assert items["0x04fc"]["item_id"] == 0x04FC
    assert items["0x15b6"]["item_id"] == 0x15B6


def test_a_tag_with_no_value_is_ignored_as_uox3_does(uox_workspace):
    uox_workspace.write_source("items.dfn", "[base_item]\n{\nid=0x0000\ndecay=\npileable=\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    item = uox_workspace.items()["base_item"]
    assert "decays" not in item
    assert "stackable" not in item


def test_colour_is_read_like_color_and_an_item_with_no_colour_writes_no_hue(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x0001]\n{\nid=0x0001\ncolour=0x44E\n}\n[0x0002]\n{\nget=0x0001\nid=0x0002\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    assert items["0x0001"]["hue"] == 0x044E
    # No colour of its own: the server's loader takes the parent's.
    assert "hue" not in items["0x0002"]


def test_reads_the_combat_fields_of_weapons_and_armor_and_the_kind_of_weapon_by_graphic(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[base_longsword]\n{\nid=0x0f60\nlayer=1\n}\n"
        "[0x0f60_t2a]\n{\nget=base_longsword\ndamage=5 33\nspd=35\nstr=25\nhp=31 90\n}\n"
        "[ringmail_tunic]\n{\nid=0x13ec\nlayer=13\ndef=22\nstr=20\nhp=41 51\n}\n"
        "[bow]\n{\nid=0x13b2\nlayer=2\ndamage=9 41\nspd=25\n}\n"
        "[mace]\n{\nid=0x0f5c\nlayer=1\ndamage=8 32\nspd=40\n}\n"
        "[spear]\n{\nid=0x0f62\nlayer=2\ndamage=2 36\nspd=50\n}\n"
        "[bardiche]\n{\nid=0x0f4d\nlayer=2\ndamage=5 49\nspd=25\n}\n"
        "[flat]\n{\nid=0x1234\nlayer=1\ndamage=3\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    # The kind goes on the item that has the graphic; its eras inherit it, and only add numbers.
    assert items["base_longsword"]["weapon_type"] == "sword"
    era = items["0x0f60_t2a"]
    assert (era["damage_min"], era["damage_max"], era["speed"], era["strength_required"], era["max_hits"]) == (5, 33, 35, 25, 90)
    assert "weapon_type" not in era
    tunic = items["ringmail_tunic"]
    assert (tunic["armor_rating"], tunic["strength_required"], tunic["max_hits"]) == (22, 20, 51)
    assert "damage_max" not in tunic
    assert [items[name]["weapon_type"] for name in ("bow", "mace", "spear", "bardiche")] == ["bow", "mace", "fencing", "pole_arm"]
    # A graphic UOX3 does not list is fought with fists: no kind, and a damage of one number is both ends.
    assert "weapon_type" not in items["flat"]
    assert (items["flat"]["damage_min"], items["flat"]["damage_max"]) == (3, 3)


def test_the_other_names_of_uox3_lodamage_hidamage_and_speed_are_read_too(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[practice_sword]\n{\nid=0x13b9\nlayer=1\nlodamage=2\nhidamage=8\nspeed=25\n}\n"
        "[both]\n{\nid=0x13b8\nlayer=1\ndamage=5 9\nlodamage=1\nhidamage=2\nspd=30\nspeed=99\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    practice, both = items["practice_sword"], items["both"]
    assert (practice["damage_min"], practice["damage_max"], practice["speed"]) == (2, 8, 25)
    # damage= and spd= win when a block has both.
    assert (both["damage_min"], both["damage_max"], both["speed"]) == (5, 9, 30)


def test_a_combat_field_that_is_not_a_number_is_left_out(uox_workspace):
    uox_workspace.write_source("items.dfn", "[sword]\n{\nid=0x0f60\nlayer=1\ndamage=lots\nspd=fast\nstr=\ndef=-3\nhp=0 0\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    sword = uox_workspace.items()["sword"]
    assert [name for name in ("damage_max", "speed", "strength_required", "armor_rating", "max_hits") if name in sword] == []


def test_numbers_may_be_hex_as_uox3_reads(uox_workspace):
    uox_workspace.write_source("items.dfn", "[ring]\n{\nid=0x108a\nlayer=0x08\nweight=0x64\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    ring = uox_workspace.items()["ring"]
    assert (ring["layer"], ring["weight"]) == ("ring", 1.0)


def test_two_handed_layer_marks_weapons_but_not_shields_or_lights(uox_workspace):
    # As UOX3: layer 2 is both hands unless the item is a shield (type=107, here from a base without id=) or a light (dir=), which go in the
    # other hand.
    uox_workspace.write_source(
        "items.dfn",
        "[base_shield]\n{\ntype=107\nlayer=0x02\n}\n"
        "[heater]\n{\nget=base_shield\nid=0x1b76\n}\n"
        "[halberd]\n{\nid=0x143e\nlayer=2\n}\n"
        "[torch]\n{\nid=0x0f6b\nlayer=2\ndir=14\n}\n"
        "[katana]\n{\nid=0x13ff\nlayer=1\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items()
    assert items["halberd"]["two_handed_weapon"] is True
    assert "two_handed_weapon" not in items["heater"]
    assert "two_handed_weapon" not in items["torch"]
    assert "two_handed_weapon" not in items["katana"]


def test_a_header_defined_twice_keeps_the_last_definition_as_uox3_does(uox_workspace):
    uox_workspace.write_source(
        "swords.dfn", "[0x2d29_lbr]\n{\nid=0x2d29\nname=machete\n}\n[0x2d29_lbr]\n{\nid=0x2d29\nname=radiant scimitar\n}\n"
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    (item,) = uox_workspace.items("swords.toml").values()
    assert item["name"] == "radiant scimitar"
    assert "Duplicate block '[0x2d29_lbr]'" in uox_workspace.error.getvalue()


def test_visible_0_is_visible_to_everyone_overriding_a_hidden_parent(uox_workspace):
    uox_workspace.write_source(
        "items.dfn", "[base_spawner]\n{\nid=0x1f14\nvisible=1\n}\n[d_woodbox_1]\n{\nget=base_spawner\nid=0x09aa\nvisible=0\n}\n"
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    assert uox_workspace.items()["d_woodbox_1"]["visibility"] == "regular"


def test_the_necromancer_sleeves_and_leggings_get_their_real_graphics(uox_workspace):
    # UOX3's leather.dfn has necro_sleeves get=0x13c6 (gloves) and necro_leggings get=0x13cc (a tunic).
    uox_workspace.write_source(
        "leather.dfn",
        "[0x13c6]\n{\nid=0x13c6\n}\n[0x13cc]\n{\nid=0x13cc\n}\n[0x13cd]\n{\nid=0x13cd\n}\n[0x13cb]\n{\nid=0x13cb\n}\n"
        "[necro_sleeves]\n{\nget=0x13c6\ncolor=0x2C3\n}\n[necro_leggings]\n{\nget=0x13cc\ncolor=0x2C3\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    items = uox_workspace.items("leather.toml")
    assert (items["necro_sleeves"]["base_id"], items["necro_leggings"]["base_id"]) == ("0x13cd", "0x13cb")


def test_with_the_scripts_source_the_items_of_a_script_with_a_lua_equivalent_get_its_script_id(uox_workspace):
    uox_workspace.write_source(
        "lighting.dfn",
        "[0x0a28]\n{\nname=candle\nid=0x0a28\n}\n\n[special_lamp]\n{\nid=0x0f00\nscript=500\n}\n\n"
        "[0x0675]\n{\nname=metal door\nid=0x0675\n}\n\n[0x0eed]\n{\nname=gold coin\nid=0x0eed\n}\n",
    )
    uox_workspace.write_scripts("jse_fileassociations.scp", "// comment\n[SCRIPT_LIST]\n{\n500=item/lights.js\n4500=item/doors.js\n}\n")
    uox_workspace.write_scripts("jse_objectassociations.scp", "[ENVOKE]\n{\n//Lights\n0x0a28=500\n0x0675=4500\n}\n")

    assert uox_workspace.run(scripts=True) == 0, uox_workspace.combined

    items = uox_workspace.items("lighting.toml")
    assert items["0x0a28_candle"]["script_id"] == "light"
    assert items["special_lamp"]["script_id"] == "light"
    assert "script_id" not in items["0x0675_metal_door"]
    assert "script_id" not in items["0x0eed_gold_coin"]


def test_a_scripts_source_without_the_associations_fails(uox_workspace):
    uox_workspace.write_source("items.dfn", "[coin]\n{\nid=0x0eed\n}\n")
    uox_workspace.scripts_source.mkdir()

    assert uox_workspace.run(scripts=True) == 2
    assert "jse_fileassociations.scp" in uox_workspace.error.getvalue()


def test_a_two_handed_layer_item_with_the_light_script_leaves_the_other_hand_free(uox_workspace):
    # A UOX3 torch has the layer of a two handed weapon and no dir=, but it is a light by its script.
    uox_workspace.write_source("lighting.dfn", "[0x0f64]\n{\nname=torch\nid=0x0f64\nlayer=2\n}\n\n[0x0f43]\n{\nname=hatchet\nid=0x0f43\nlayer=2\n}\n")
    uox_workspace.write_scripts("jse_fileassociations.scp", "[SCRIPT_LIST]\n{\n500=item/lights.js\n}\n")
    uox_workspace.write_scripts("jse_objectassociations.scp", "[ENVOKE]\n{\n//Lights\n0x0f64=500\n}\n")

    assert uox_workspace.run(scripts=True) == 0, uox_workspace.combined

    items = uox_workspace.items("lighting.toml")
    assert items["0x0f64_torch"]["script_id"] == "light"
    assert "two_handed_weapon" not in items["0x0f64_torch"]
    assert items["0x0f43_hatchet"]["two_handed_weapon"] is True


def test_the_carpentry_tools_get_the_carpentry_tool_script_by_their_graphic(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[0x1034]\n{\nname=saw\nid=0x1034\n}\n"
        "[0x10e5]\n{\nname=froe\nid=0x10e5\n}\n"
        "[0x102e]\n{\nname=nails\nid=0x102e\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a carpenter works wood with: scripts/items/carpentry_tool.lua. Nails are no tool.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert by_name["saw"]["script_id"] == "carpentry_tool"
    assert by_name["froe"]["script_id"] == "carpentry_tool"
    assert "script_id" not in by_name["nails"]


def test_the_smithing_tools_get_the_smithing_tool_script_by_their_graphic(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[0x13e3]\n{\nname=smith's hammer\nid=0x13e3\n}\n"
        "[0x0fbb]\n{\nname=tongs\nid=0x0fbb\n}\n"
        "[0x0fb4]\n{\nname=sledge hammer\nid=0x0fb4\n}\n"
        "[0x0faf]\n{\nname=anvil\nid=0x0faf\n}\n"
        "[prospectors_tool]\n{\nname=prospector's tool\nid=0x0fb4\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a smith forges with at an anvil: scripts/items/smithing_tool.lua. An anvil is no tool.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert [by_name[name].get("script_id") for name in ("smith's hammer", "tongs", "sledge hammer", "anvil", "prospector's tool")] == [
        "smithing_tool", "smithing_tool", "smithing_tool", None, None
    ]


def test_the_sewing_kits_get_the_tailoring_tool_script_but_not_the_scissors(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[0x0f9d]\n{\nname=sewing kit\nid=0x0f9d\n}\n"
        "[0x0f9e]\n{\nname=scissors\nid=0x0f9e\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a tailor sews with: scripts/items/tailoring_tool.lua. Scissors cut, they do not sew.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert by_name["sewing kit"]["script_id"] == "tailoring_tool"
    assert "script_id" not in by_name["scissors"]


def test_the_cooking_tools_get_the_cooking_tool_script(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[0x097f]\n{\nname=skillet\nid=0x097f\n}\n"
        "[0x103e]\n{\nname=sifter\nid=0x103e\n}\n"
        "[0x1043]\n{\nname=rolling pin\nid=0x1043\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a cook works with: scripts/items/cooking_tool.lua.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert [by_name[name].get("script_id") for name in ("skillet", "sifter", "rolling pin")] == ["cooking_tool"] * 3


def test_the_fletcher_tools_get_the_fletching_tool_script(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x1022]\n{\nname=fletcher's tools\nid=0x1022\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a bowyer works with: scripts/items/fletching_tool.lua.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert by_name["fletcher's tools"]["script_id"] == "fletching_tool"


def test_the_tinker_tools_get_the_tinkering_tool_script(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[0x1ebc]\n{\nname=tinker's tools\nid=0x1ebc\n}\n"
        "[0x1eb8]\n{\nname=tool kit\nid=0x1eb8\n}\n"
        "[taxidermykit]\n{\nname=a taxidermy kit\nid=0x1eba\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a tinker works with: scripts/items/tinkering_tool.lua.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert by_name["tinker's tools"]["script_id"] == "tinkering_tool"
    assert by_name["tool kit"]["script_id"] == "tinkering_tool"
    # The taxidermy kit shares a tool kit's graphic but stuffs trophies: no tinkering with it.
    assert "script_id" not in by_name["a taxidermy kit"]


def test_a_preset_map_gets_its_area_and_the_map_script(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[britainmap]\n{\nname=britain map\nid=0x14ec\ncustominttag=Map 3\n}\n"
        "[malasmap]\n{\nname=malas map\nid=0x14ec\ncustominttag=Map 31\n}\n"
        "[craftedcitymap]\n{\nname=city map\nid=0x14ec\ncustominttag=Map 50\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What opens a map: scripts/items/map_item.lua, on the area UOX3's preset table gives it.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    britain = by_name["britain map"]
    assert britain["script_id"] == "map_item"
    assert {key: britain["tags"][key] for key in britain["tags"] if key.startswith("map_")} == {
        "map_x1": "1092", "map_y1": "1396", "map_x2": "1736", "map_y2": "1924", "map_width": "200", "map_height": "200",
        "map_facet": "0",
    }
    assert by_name["malas map"]["tags"]["map_facet"] == "3"
    # A crafted map is drawn by its cartographer: no area of its own, the script all the same.
    assert by_name["city map"]["script_id"] == "map_item" and "map_x1" not in by_name["city map"]["tags"]


def test_a_blank_map_gets_the_map_script_so_it_says_it_is_blank(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x14ec]\n{\nname=blank map\nid=0x14ec\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert by_name["blank map"]["script_id"] == "map_item"


def test_the_pens_get_the_cartography_tool_script(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x0fc0]\n{\nname=pen and ink\nid=0x0fc0\n}\n")

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a cartographer draws with: scripts/items/cartography_tool.lua.
    by_name = {item.get("name"): item for item in uox_workspace.items().values()}
    assert by_name["pen and ink"]["script_id"] == "cartography_tool"


def test_the_potions_a_player_drinks_get_the_potion_script_and_explosion_waits(uox_workspace):
    uox_workspace.write_source(
        "items.dfn",
        "[healpotion]\n{\nname=yellow potion\nid=0x0f0c\n}\n"
        "[nightsightpotion]\n{\nname=black potion\nid=0x0f06\n}\n"
        "[poisonpotion]\n{\nname=green potion\nid=0x0f0a\n}\n"
        "[curepotion]\n{\nname=orange potion\nid=0x0f07\n}\n"
        "[explosionpotion]\n{\nname=purple potion\nid=0x0f0d\n}\n",
    )

    assert uox_workspace.run() == 0, uox_workspace.combined

    # What a player drinks: scripts/items/potion.lua. Explosion potions are thrown, and come with throwing.
    items = uox_workspace.items()
    assert [items[name]["script_id"] for name in ("healpotion", "nightsightpotion", "poisonpotion", "curepotion")] == ["potion"] * 4
    assert "script_id" not in items["explosionpotion"]
