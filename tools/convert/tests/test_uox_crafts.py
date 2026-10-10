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
    assert gorget["resources"] == [{"resource": "0x1bf2_iron_ingot", "amount": 12}, {"resource": "0x175f_folded_cloth", "amount": 3}]
    assert gorget["skills"] == [{"skill": "tailoring", "min": 20.0, "max": 45.0}]
    assert smithing["group"][1]["recipe"][0]["item"] == "0x1b73_buckler"


def test_every_skill_name_is_one_of_the_server(tmp_path):
    import re
    from pathlib import Path

    skills = Path(__file__).resolve().parents[3] / "moongate_root" / "data" / "skills.toml"
    ids = set(re.findall(r'^id = "([a-z_]+)"', skills.read_text(), re.M))

    assert [name for name in crafts.SKILL_NAMES.values() if name not in ids] == []
