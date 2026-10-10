"""The ``uox-crafts`` command: UOX3's create menus into data/crafts."""

from __future__ import annotations

import io
import tomllib
from pathlib import Path

from moongate_convert import crafts

ITEMS = """
[[item]]
id = "0x0a2a"
[[item]]
id = "0x0b2f_wooden_throne"
[[item]]
id = "0x1bd7_board"
[[item]]
id = "0x1bda_board"
[[item]]
id = "0x1be0_log"
[[item]]
id = "0x1eb1_barrel_staves"
[[item]]
id = "0x0eb3_lute"
[[item]]
id = "0x175d_cloth"
[[item]]
id = "0x0e7f"
[[item]]
id = "0x0a51"
[[item]]
id = "smallforgedeed"
"""

RESOURCES = "[RESOURCE WOOD]\n{\nID=0x1bd7\nID=0x1bda\nID=0x1bdd\nID=0x1be0\n}\n[RESOURCE CLOTH]\n{\nID=0x175d\n}\n[RESOURCE GEMS]\n{\nID=0x0f0f\n}\n"

CARPENTRY = """
[SUBMENU 19]
{
MENU=20
MENU=22
MENU=25
MENU=27
}
[MENUENTRY 20]
{
NAME=Chairs
SUBMENU=20
}
[MENUENTRY 22]
{
NAME=Other Items
SUBMENU=22
}
[MENUENTRY 25]
{
NAME=Musical items
SUBMENU=25
}
[MENUENTRY 27]
{
NAME=Blacksmith Add-ons
SUBMENU=27
}
[SUBMENU 20]
{
ITEM=50
ITEM=51
MENU=19
}
[SUBMENU 22]
{
ITEM=60
ITEM=61
ITEM=62
ITEM=63
MENU=19
}
[SUBMENU 25]
{
ITEM=70
MENU=19
}
[SUBMENU 27]
{
ITEM=80
MENU=19
}
[ITEM 50]
{
NAME=Barstool
RESOURCE=WOOD 9
SKILL=11 110 360
ADDITEM=0x0a2a
}
[ITEM 51]
{
NAME=Magincian Throne
RESOURCE=WOOD 19
SKILL=11 736 986
ADDITEM=0x0b2f
}
[ITEM 60]
{
NAME=Boards
RESOURCE=LOGS 1
SKILL=11 0 250
ADDITEM=0x1bd7
}
[ITEM 61]
{
NAME=Open Keg
RESOURCE=0x1eb1 3
SKILL=11 578 828
ADDITEM=0x0e7f
}
[ITEM 62]
{
NAME=Armoire
RESOURCE=0x1be0 35
SKILL=11 841 1091
ADDITEM=0x0a51
}
[ITEM 63]
{
NAME=Lute
RESOURCE=WOOD 25
RESOURCE=CLOTH 10
SKILL=11 684 934
SKILL=29 450 700
ADDITEM=0x0eb3
}
[ITEM 70]
{
NAME=Lute
RESOURCE=WOOD 25
SKILL=11 684 934
ADDITEM=0x0eb3
}
[ITEM 80]
{
NAME=Small Forge
RESOURCE=WOOD 5
SKILL=11 736 986
ADDITEM=smallforgedeed
}
"""


def convert(tmp_path: Path, carpentry: str = CARPENTRY) -> tuple[int, dict, dict, str]:
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text(RESOURCES)
    (source / "carpentry.dfn").write_text(carpentry)
    (items / "all.toml").write_text(ITEMS)
    output, error = io.StringIO(), io.StringIO()
    code = crafts.run(source, items, destination, output, error)
    read = lambda name: tomllib.loads((destination / name).read_text()) if (destination / name).exists() else {}

    return code, read("carpentry.toml"), read("resources.toml"), error.getvalue()


def test_the_groups_keep_their_order_and_the_add_ons_and_boards_are_left_out(tmp_path):
    code, carpentry, _, error = convert(tmp_path)

    assert code == 0, error
    assert (carpentry["id"], carpentry["name"], carpentry["skill"], carpentry["sound"]) == ("carpentry", "Carpentry", "carpentry", 0x023D)
    assert [group["name"] for group in carpentry["group"]] == ["Chairs", "Other Items", "Musical items"]
    assert [recipe["name"] for recipe in carpentry["group"][1]["recipe"]] == ["Open Keg", "Armoire", "Lute"]


def test_a_recipe_has_its_item_skills_in_points_and_resources(tmp_path):
    _, carpentry, _, _ = convert(tmp_path)
    chairs = carpentry["group"][0]["recipe"]

    assert chairs[0] == {"name": "Barstool", "item": "0x0a2a", "skill_min": 11.0, "skill_max": 36.0, "resources": [{"resource": "wood", "amount": 9}], "skills": []}
    # The item resolves to the template that starts with its graphic; the misspelt name is fixed.
    assert (chairs[1]["name"], chairs[1]["item"], chairs[1]["skill_min"], chairs[1]["skill_max"]) == ("Magician Throne", "0x0b2f_wooden_throne", 73.6, 98.6)


def test_raw_graphics_become_templates_the_raw_logs_become_wood_and_second_skills_are_kept(tmp_path):
    _, carpentry, _, _ = convert(tmp_path)
    keg, armoire, lute = carpentry["group"][1]["recipe"]

    assert keg["resources"] == [{"resource": "0x1eb1_barrel_staves", "amount": 3}]
    assert armoire["resources"] == [{"resource": "wood", "amount": 35}]
    assert lute["resources"] == [{"resource": "wood", "amount": 25}, {"resource": "cloth", "amount": 10}]
    assert lute["skills"] == [{"skill": "musicianship", "min": 45.0, "max": 70.0}]


def test_the_resource_lists_resolve_their_graphics_and_wood_is_boards_only(tmp_path):
    _, _, resources, error = convert(tmp_path)
    lists = {entry["id"]: entry["templates"] for entry in resources["resource"]}

    assert lists["wood"] == ["0x1bd7_board", "0x1bda_board"]
    assert lists["cloth"] == ["0x175d_cloth"]
    # No template has the graphic of the gems: the list is left out, with a warning.
    assert "gems" not in lists and "gems" in error


def test_an_unknown_skill_or_an_item_without_template_stops_the_conversion(tmp_path):
    code, _, _, error = convert(tmp_path, CARPENTRY.replace("SKILL=29 450 700", "SKILL=99 450 700"))
    assert code == 2 and "99" in error

    (tmp_path / "again").mkdir()
    code, _, _, error = convert(tmp_path / "again", CARPENTRY.replace("ADDITEM=0x0a2a", "ADDITEM=0x7777"))
    assert code == 2 and "0x7777" in error


SMITHING = """
[SUBMENU 1]
{
MENU=1
MENU=2
MENU=3
}
[MENUENTRY 1]
{
NAME=Armor
SUBMENU=2
}
[MENUENTRY 2]
{
NAME=Shields
SUBMENU=3
}
[MENUENTRY 3]
{
NAME=Ringmail
SUBMENU=4
}
[SUBMENU 2]
{
MENU=3
MENU=0
}
[MENUENTRY 0]
{
NAME=Previous Menu
SUBMENU=1
}
[SUBMENU 3]
{
ITEM=1
MENU=0
}
[SUBMENU 4]
{
ITEM=2
ITEM=3
MENU=1
}
[ITEM 1]
{
NAME=buckler
RESOURCE=METAL 10
SKILL=7 0 100
ADDITEM=0x1b73
}
[ITEM 2]
{
NAME=ringmail gloves
RESOURCE=METAL 10
SKILL=7 122 372
ADDITEM=0x13eb
}
[ITEM 3]
{
NAME=studded gorget
RESOURCE=0x1bf2 12
RESOURCE=0x175f 3
SKILL=7 300 550
SKILL=34 200 450
ADDITEM=0x13eb
}
"""

SMITH_ITEMS = """
[[item]]
id = "0x1b73_buckler"
[[item]]
id = "0x13eb_ringmail_gloves"
[[item]]
id = "0x1bf2_iron_ingot"
[[item]]
id = "0x1bef_iron_ingot"
[[item]]
id = "0x175f_folded_cloth"
"""


def test_blacksmithing_walks_the_nested_menus_into_groups_in_their_order(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE METAL]\n{\nID=0x1bf2\nID=0x1bef\n}\n")
    (source / "smithing.dfn").write_text(SMITHING)
    (items / "all.toml").write_text(SMITH_ITEMS)
    output, error = io.StringIO(), io.StringIO()

    assert crafts.run(source, items, destination, output, error) == 0, error.getvalue()

    smithing = tomllib.loads((destination / "blacksmithing.toml").read_text())
    assert (smithing["id"], smithing["name"], smithing["skill"], smithing["sound"]) == ("blacksmithing", "Blacksmithing", "blacksmithy", 0x002A)
    # Armor holds the menu Ringmail; Shields holds a recipe: one group a menu with recipes, back links not followed.
    assert [group["name"] for group in smithing["group"]] == ["Ringmail", "Shields"]
    gloves, gorget = smithing["group"][0]["recipe"]
    assert (gloves["item"], gloves["skill_min"], gloves["skill_max"]) == ("0x13eb_ringmail_gloves", 12.2, 37.2)
    # The gump shows a name as a title: UOX3 writes these in lower case.
    assert gloves["name"] == "Ringmail gloves"
    assert gorget["resources"] == [{"resource": "metal", "amount": 12}, {"resource": "0x175f_folded_cloth", "amount": 3}]
    assert gorget["skills"] == [{"skill": "tailoring", "min": 20.0, "max": 45.0}]
    assert smithing["group"][1]["recipe"][0]["item"] == "0x1b73_buckler"


def test_every_skill_name_is_one_of_the_server(tmp_path):
    import re
    from pathlib import Path

    skills = Path(__file__).resolve().parents[3] / "moongate_root" / "data" / "skills.toml"
    ids = set(re.findall(r'^id = "([a-z_]+)"', skills.read_text(), re.M))

    assert [name for name in crafts.SKILL_NAMES.values() if name not in ids] == []


def test_a_raw_graphic_of_a_resource_list_becomes_that_list(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE METAL]\n{\nID=0x1bf2\nID=0x1bef\n}\n[RESOURCE CLOTH]\n{\nID=0x175f\n}\n")
    (source / "smithing.dfn").write_text(SMITHING)
    (items / "all.toml").write_text(SMITH_ITEMS)

    assert crafts.run(source, items, destination, io.StringIO(), io.StringIO()) == 0

    # UOX3 names some ingots and cloth by their graphic: they are the lists, so the player reads which one lacks.
    gorget = tomllib.loads((destination / "blacksmithing.toml").read_text())["group"][0]["recipe"][1]
    assert gorget["resources"] == [{"resource": "metal", "amount": 12}, {"resource": "cloth", "amount": 3}]


def test_a_menu_entry_without_its_submenu_is_a_conversion_error(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE METAL]\n{\nID=0x1bf2\n}\n")
    (source / "smithing.dfn").write_text(SMITHING.replace("[SUBMENU 3]", "[SUBMENU 33]"))
    (items / "all.toml").write_text(SMITH_ITEMS)
    error = io.StringIO()

    assert crafts.run(source, items, destination, io.StringIO(), error) == 2
    assert "SUBMENU 3" in error.getvalue()


def test_the_crafts_the_converter_knows_and_their_sounds():
    # Each craft: its id, the name its gump shows, its skill and the root menu of UOX3's file.
    assert crafts.CRAFTS["tailoring"] == ("tailoring", "Tailoring", "tailoring", 39)
    assert crafts.SOUNDS["tailoring"] == 0x0248


def test_tinkering_is_known_its_traps_left_out_and_its_misspelt_group_fixed():
    assert crafts.CRAFTS["tinkering"] == ("tinkering", "Tinkering", "tinkering", 59)
    assert crafts.SOUNDS["tinkering"] == 0x023B
    # The traps of tinkering arm containers, which cannot be trapped yet; UOX3's file lacks their menu too.
    assert "traps" in crafts.SKIPPED_GROUPS
    assert crafts.GROUP_FIXES["Miscellaneuos"] == "Miscellaneous"


def test_a_skill_line_uox3_got_wrong_is_fixed():
    # UOX3 writes the scales of tinkering as 63.8 to 11.4: a zero is missing from the most.
    assert crafts.SKILL_FIXES[("tinkering", "scales")] == "37 638 1140"
    assert crafts.SKILL_FIXES[("tinkering", "heating stand")] == "37 643 1140"


BOWCRAFT = """
[SUBMENU 49]
{
ITEM=190
MENU=50
ITEM=191
}
[MENUENTRY 49]
{
NAME=Previous Menu
SUBMENU=49
}
[MENUENTRY 50]
{
NAME=Shafts
SUBMENU=50
}
[SUBMENU 50]
{
ITEM=194
ITEM=195
MENU=49
}
[SUBMENU 51]
{
MENU=52
}
[MENUENTRY 52]
{
NAME=Arrows
SUBMENU=52
}
[SUBMENU 52]
{
ITEM=198
}
[ITEM 190]
{
NAME=kindling
RESOURCE=WOOD 1
SKILL=8 0 500
ADDITEM=0x0de1
}
[ITEM 191]
{
NAME=bow
RESOURCE=WOOD 7
SKILL=8 300 700
ADDITEM=0x13b2
}
[ITEM 194]
{
NAME=one shaft
RESOURCE=WOOD 1
SKILL=8 0 400
ADDITEM=0x1bd4
}
[ITEM 195]
{
NAME=five shafts
RESOURCE=WOOD 5
SKILL=8 0 400
ADDITEM=0x1bd4,5
}
[ITEM 198]
{
NAME=one arrow
RESOURCE=0x1bd4 1
RESOURCE=0x1bd1 1
SKILL=8 0 400
ADDITEM=0x0f3f
}
"""

BOWCRAFT_ITEMS = """
[[item]]
id = "0x1bd7_board"
[[item]]
id = "0x0de1_kindling"
[[item]]
id = "0x13b2_bow"
[[item]]
id = "0x1bd4_shaft"
[[item]]
id = "0x1bd4_5_shaft"
[[item]]
id = "0x1bd1_feather"
[[item]]
id = "0x0f3f_arrow"
[[item]]
id = "0x0f3f_5"
"""


def test_fletching_takes_the_bows_of_its_root_and_the_menus_of_the_fletching_tool(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE WOOD]\n{\nID=0x1bd7\n}\n")
    (source / "bowcraft.dfn").write_text(BOWCRAFT)
    (items / "all.toml").write_text(BOWCRAFT_ITEMS)
    output, error = io.StringIO(), io.StringIO()

    assert crafts.run(source, items, destination, output, error) == 0, error.getvalue()

    fletching = tomllib.loads((destination / "fletching.toml").read_text())
    assert (fletching["name"], fletching["skill"], fletching["sound"]) == ("Bowcraft and Fletching", "bowcraft_fletching", 0x0055)
    # The root's own recipes are the bows; UOX3 opens arrows and bolts from the fletching tool, a second root.
    assert [group["name"] for group in fletching["group"]] == ["Weapons", "Shafts", "Arrows"]
    # Kindling is what an axe already makes; a batch of five is the recipe of one made five times.
    assert [recipe["name"] for recipe in fletching["group"][0]["recipe"]] == ["Bow"]
    assert [recipe["name"] for recipe in fletching["group"][1]["recipe"]] == ["Shaft"]
    arrow = fletching["group"][2]["recipe"][0]
    assert (arrow["name"], arrow["item"]) == ("Arrow", "0x0f3f_arrow")
    # The stacks of five shafts or arrows share the graphic: the recipe makes and takes the single one.
    assert arrow["resources"] == [{"resource": "0x1bd4_shaft", "amount": 1}, {"resource": "0x1bd1_feather", "amount": 1}]


TINKERING = """
[SUBMENU 59]
{
MENU=60
MENU=61
}
[MENUENTRY 60]
{
NAME=Tools
SUBMENU=60
}
[MENUENTRY 61]
{
NAME=Miscellaneuos
SUBMENU=61
}
[SUBMENU 60]
{
ITEM=1
ITEM=2
ITEM=3
}
[SUBMENU 61]
{
ITEM=4
}
[ITEM 1]
{
NAME=tinker's tools
RESOURCE=METAL 2
SKILL=37 100 600
ADDITEM=0x1eb9
}
[ITEM 2]
{
NAME=spoon
RESOURCE=METAL 1
SKILL=37 0 500
ADDITEM=0x09f8
}
[ITEM 3]
{
NAME=spoon
RESOURCE=METAL 1
SKILL=37 0 500
ADDITEM=0x09f9
}
[ITEM 4]
{
NAME=scales
RESOURCE=METAL 4
SKILL=37 638 114
ADDITEM=0x1851
}
"""

TINKER_ITEMS = """
[[item]]
id = "0x1bf2_iron_ingot"
[[item]]
id = "0x1eb9_tool_kit"
[[item]]
id = "0x1ebc_tinker's_tools"
[[item]]
id = "0x09f8_spoon"
[[item]]
id = "0x09f9_spoon"
[[item]]
id = "0x1851_scales"
"""


def test_tinkering_fixes_what_uox3_writes_wrong(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE METAL]\n{\nID=0x1bf2\n}\n")
    (source / "tinkering.dfn").write_text(TINKERING)
    (items / "all.toml").write_text(TINKER_ITEMS)
    output, error = io.StringIO(), io.StringIO()

    assert crafts.run(source, items, destination, output, error) == 0, error.getvalue()

    tinkering = tomllib.loads((destination / "tinkering.toml").read_text())
    assert [group["name"] for group in tinkering["group"]] == ["Tools", "Miscellaneous"]
    tools, spoon, other_spoon = tinkering["group"][0]["recipe"]
    # UOX3 makes the heavy tool kit; the recipe is named for the tinker's tools.
    assert tools["item"] == "0x1ebc_tinker's_tools"
    # Two recipes of one name are told apart in the gump.
    assert (spoon["name"], other_spoon["name"]) == ("Spoon", "Spoon 2")
    scales = tinkering["group"][1]["recipe"][0]
    assert (scales["skill_min"], scales["skill_max"]) == (63.8, 114.0)


COOKING = """
[SUBMENU 750]
{
MENU=2001
MENU=2003
}
[MENUENTRY 2001]
{
NAME=Ingredients
SUBMENU=2001
}
[MENUENTRY 2003]
{
NAME=Baking
SUBMENU=2003
}
[SUBMENU 2001]
{
ITEM=1501
ITEM=1503
}
[SUBMENU 2003]
{
ITEM=1605
ITEM=1606
ITEM=1651
}
[ITEM 1501]
{
NAME=dough
RESOURCE=FLOUR 1
SKILL=13 0 1000
ADDITEM=0x103d
}
[ITEM 1503]
{
NAME=cake mix
RESOURCE=FLOUR 1
RESOURCE=0x103d 1 0x96
SKILL=13 0 1000
ADDITEM=cake_mix
}
[ITEM 1605]
{
NAME=baked meat pie
RESOURCE=0x1042
SKILL=13 0 1000
ADDITEM=baked_meat_pie
}
[ITEM 1651]
{
NAME=chicken leg
RESOURCE=0x1607
SKILL=13 0 1000
ADDITEM=0x1608
}
[ITEM 1606]
{
NAME=sausage pizza
RESOURCE=0x1042 1 0x0
SKILL=13 0 1000
ADDITEM=sausage_pizza
}
"""

COOKING_ITEMS = """
[[item]]
id = "0x1039_sack_of_flour"
[[item]]
id = "0x1045_sack_of_flour"
[[item]]
id = "0x103a_open_sack_of_flour"
[[item]]
id = "0x103d_dough"
[[item]]
id = "sweet_dough"
[[item]]
id = "cake_mix"
[[item]]
id = "0x1042_unbaked_pie"
[[item]]
id = "unbaked_meat_pie"
[[item]]
id = "uncooked_sausage_pizza"
[[item]]
id = "baked_meat_pie"
[[item]]
id = "sausage_pizza"
[[item]]
id = "0x1607_raw_chicken_leg"
[[item]]
id = "0x1608_chicken_leg"
[[item]]
id = "0x09f1_cut_of_raw_ribs"
"""


def test_cooking_takes_the_dough_or_pie_uox3_tells_apart_by_hue_and_more(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE FLOUR]\n{\nID=0x103a\n}\n[RESOURCE RAWMEAT]\n{\nID=0x09f1\nID=0x1607\n}\n")
    (source / "cooking.dfn").write_text(COOKING)
    (items / "all.toml").write_text(COOKING_ITEMS)
    output, error = io.StringIO(), io.StringIO()

    assert crafts.run(source, items, destination, output, error) == 0, error.getvalue()

    cooking = tomllib.loads((destination / "cooking.toml").read_text())
    assert (cooking["name"], cooking["skill"], cooking["sound"]) == ("Cooking", "cooking", 0x0057)
    assert [group["name"] for group in cooking["group"]] == ["Ingredients", "Baking"]
    dough, cake_mix = cooking["group"][0]["recipe"]
    assert dough["item"] == "0x103d_dough"
    # Sweet dough is dough of hue 0x96 in UOX3; Moongate has a template of its own.
    assert cake_mix["resources"] == [{"resource": "flour", "amount": 1}, {"resource": "sweet_dough", "amount": 1}]
    meat_pie, pizza, chicken_leg = cooking["group"][1]["recipe"]
    # The raw chicken leg is in UOX3's list of raw meat: a chicken leg is not cooked from ribs.
    assert chicken_leg["resources"] == [{"resource": "0x1607_raw_chicken_leg", "amount": 1}]

    # UOX3 opens a closed sack of flour by a script; here a closed sack is flour as it is.
    flour = {entry["id"]: entry["templates"] for entry in tomllib.loads((destination / "resources.toml").read_text())["resource"]}["flour"]
    assert flour == ["0x1039_sack_of_flour", "0x103a_open_sack_of_flour", "0x1045_sack_of_flour"]

    # An unbaked pie is told apart by MORE, and a resource without an amount is one; UOX3 bakes the sausage pizza from the quiche: it takes the uncooked pizza.
    assert meat_pie["resources"] == [{"resource": "unbaked_meat_pie", "amount": 1}]
    assert pizza["resources"] == [{"resource": "uncooked_sausage_pizza", "amount": 1}]


def test_a_resource_fix_naming_no_template_stops_the_conversion(tmp_path, monkeypatch):
    monkeypatch.setitem(crafts.RESOURCE_FIXES, ("cooking", "sausage pizza", "0x1042"), "no_such_pizza")
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE FLOUR]\n{\nID=0x103a\n}\n[RESOURCE RAWMEAT]\n{\nID=0x09f1\nID=0x1607\n}\n")
    (source / "cooking.dfn").write_text(COOKING)
    (items / "all.toml").write_text(COOKING_ITEMS)
    output, error = io.StringIO(), io.StringIO()

    assert crafts.run(source, items, destination, output, error) == 2
    assert "no_such_pizza" in error.getvalue()


CARTOGRAPHY = """
[SUBMENU 80]
{
ITEM=2000
ITEM=2003
ITEM=2004
}
[ITEM 2000]
{
NAME=local map
RESOURCE=MAPS 1
SKILL=12 0 50
ADDITEM=craftedlocalmap
}
[ITEM 2003]
{
NAME=world map
RESOURCE=MAPS 1
SKILL=12 980 1500
ADDITEM=largeworldmap
}
[ITEM 2004]
{
NAME=world map
RESOURCE=MAPS 1
SKILL=12 980 1500
ADDITEM=ilshenarmap
}
"""

CARTOGRAPHY_ITEMS = """
[[item]]
id = "0x0e34_a_blank_scroll"
[[item]]
id = "0x14eb_map"
[[item]]
id = "0x14ec_blank_map"
[[item]]
id = "craftedlocalmap"
[[item]]
id = "largeworldmap"
[[item]]
id = "ilshenarmap"
"""


def test_cartography_has_the_classic_numbers_names_by_facet_and_takes_blank_maps(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE MAPS]\n{\nID=0x14eb\nID=0x0e34\n}\n")
    (source / "cartography.dfn").write_text(CARTOGRAPHY)
    (items / "all.toml").write_text(CARTOGRAPHY_ITEMS)
    output, error = io.StringIO(), io.StringIO()

    assert crafts.run(source, items, destination, output, error) == 0, error.getvalue()

    cartography = tomllib.loads((destination / "cartography.toml").read_text())
    assert (cartography["name"], cartography["skill"], cartography["sound"]) == ("Cartography", "cartography", 0x0249)
    # The root menu holds the recipes themselves.
    [group] = cartography["group"]
    assert group["name"] == "Maps"
    local, world, ilshenar = group["recipe"]
    # UOX3 writes 0 to 5 for the local map and 98 to 150 for the world maps: the classic numbers instead.
    assert (local["name"], local["skill_min"], local["skill_max"]) == ("Local map", 10.0, 70.0)
    assert (world["name"], world["skill_min"], world["skill_max"]) == ("World map", 39.5, 99.5)
    assert ilshenar["name"] == "World map of Ilshenar"
    # A blank map, as vendors sell it, is a map to draw on; a blank scroll is the scribe's.
    lists = {entry["id"]: entry["templates"] for entry in tomllib.loads((destination / "resources.toml").read_text())["resource"]}
    assert lists["maps"] == ["0x14eb_map", "0x14ec_blank_map"]


ALCHEMY = """
[SUBMENU 89]
{
MENU=93
}
[MENUENTRY 93]
{
NAME=Healing Potions
SUBMENU=93
}
[SUBMENU 93]
{
ITEM=298
ITEM=299
}
[ITEM 298]
{
NAME=Lesser Heal
RESOURCE=0x0f85 1
SKILL=0 0 500
ADDITEM=0x0F0C
}
[ITEM 299]
{
NAME=Heal
RESOURCE=0x0f85 3
SKILL=0 151 650
ADDITEM=0x0F0C-b
}
"""

ALCHEMY_ITEMS = """
[[item]]
id = "0x0f85_ginseng"
[[item]]
id = "0x0f85_10_ginseng"
[[item]]
id = "0x0f0c_yellow_potion"
[[item]]
id = "0x0f0c_b_yellow_potion"
[[item]]
id = "0x0f0e_empty_bottle"
"""


def test_alchemy_makes_the_potions_vendors_sell_each_in_an_empty_bottle(tmp_path):
    source, items, destination = tmp_path / "create", tmp_path / "items", tmp_path / "crafts"
    source.mkdir()
    items.mkdir()
    (source / "resources.dfn").write_text("")
    (source / "alchemy.dfn").write_text(ALCHEMY)
    (items / "all.toml").write_text(ALCHEMY_ITEMS)
    output, error = io.StringIO(), io.StringIO()

    assert crafts.run(source, items, destination, output, error) == 0, error.getvalue()

    alchemy = tomllib.loads((destination / "alchemy.toml").read_text())
    assert (alchemy["name"], alchemy["skill"], alchemy["sound"]) == ("Alchemy", "alchemy", 0x0242)
    lesser, heal = alchemy["group"][0]["recipe"]
    # UOX3 names the plain potion by its graphic, the stronger one with a letter after a dash.
    assert (lesser["item"], heal["item"]) == ("0x0f0c_yellow_potion", "0x0f0c_b_yellow_potion")
    # Ginseng is sold one by one and ten at a time: both are the reagent, a list of its own.
    assert heal["resources"] == [{"resource": "ginseng", "amount": 3}, {"resource": "0x0f0e_empty_bottle", "amount": 1}]
    lists = {entry["id"]: entry["templates"] for entry in tomllib.loads((destination / "resources.toml").read_text())["resource"]}
    assert lists["ginseng"] == ["0x0f85_10_ginseng", "0x0f85_ginseng"]
    assert (heal["skill_min"], heal["skill_max"]) == (15.1, 65.0)
