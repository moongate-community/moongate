"""The ``uox-crafts`` command with ``--spells``: the craft of inscription, built from data/spells.toml."""

from __future__ import annotations

import io
import tomllib
from pathlib import Path

from moongate_convert import crafts
from moongate_convert.cli import main

REPOSITORY = Path(__file__).resolve().parents[3]

ITEMS = """
[[item]]
id = "0x0e34_a_blank_scroll"
[[item]]
id = "0x0f7b_blood_moss"
[[item]]
id = "0x0f7b_10_blood_moss"
[[item]]
id = "0x0f88_nightshade"
[[item]]
id = "0x0f7a_black_pearl"
[[item]]
id = "0x1f2e_clumsy_scroll"
[[item]]
id = "0x1f3e_fireball_scroll"
[[item]]
id = "0x1f6c_water_scroll"
"""


def spell(number: int, key: str, circle: int, scroll: str, reagents: list[tuple[str, int]], extra: str = "") -> str:
    held = ", ".join(f'{{ template = "{template}", amount = {amount} }}' for template, amount in reagents)

    return (
        f'[[spell]]\nid = {number}\nkey = "{key}"\nname = "{key.replace("_", " ").title()}"\ncircle = {circle}\nreagents = [{held}]\n'
        f'scroll = "{scroll}"\n{extra}'
    )


SPELLS = "\n".join(
    [
        spell(1, "clumsy", 1, "0x1f2e_clumsy_scroll", [("0x0f7b_blood_moss", 1), ("0x0f88_nightshade", 1)]),
        spell(2, "fireball", 3, "0x1f3e_fireball_scroll", [("0x0f7a_black_pearl", 1)]),
        spell(64, "summon_water_elemental", 8, "0x1f6c_water_scroll", [("0x0f7b_blood_moss", 1)]),
        spell(3, "off", 2, "0x1f2e_clumsy_scroll", [("0x0f88_nightshade", 1)], "enabled = false\n"),
    ]
)


def convert(tmp_path: Path, spells_text: str | None = SPELLS) -> tuple[int, str, str, Path]:
    source = tmp_path / "create"
    source.mkdir()
    (source / "resources.dfn").write_text("[RESOURCE WOOD]\n{\nID=0x1bd7\n}\n", encoding="utf-8")
    items = tmp_path / "items"
    items.mkdir()
    (items / "all.toml").write_text(ITEMS + '[[item]]\nid = "0x1bd7_board"\n', encoding="utf-8")
    destination = tmp_path / "crafts"
    arguments = ["uox-crafts", "--source", str(source), "--items", str(items), "--destination", str(destination)]

    if spells_text is not None:
        (tmp_path / "spells.toml").write_text(spells_text, encoding="utf-8")
        arguments += ["--spells", str(tmp_path / "spells.toml")]

    out, err = io.StringIO(), io.StringIO()
    code = main(arguments, out, err)

    return code, out.getvalue(), err.getvalue(), destination


def test_a_scroll_for_each_enabled_spell_in_the_groups_of_its_circles(tmp_path):
    code, _, error, destination = convert(tmp_path)

    assert code == 0, error
    craft = tomllib.loads((destination / "inscription.toml").read_text(encoding="utf-8"))
    assert (craft["id"], craft["name"], craft["skill"], craft["sound"]) == ("inscription", "Inscription", "inscription", 0x0249)
    assert [group["name"] for group in craft["group"]] == ["First Circle", "Third Circle", "Eighth Circle"]
    clumsy = craft["group"][0]["recipe"][0]
    assert (clumsy["name"], clumsy["item"], clumsy["spell"], clumsy["mana"]) == ("Clumsy", "0x1f2e_clumsy_scroll", "clumsy", 4)
    assert [recipe["spell"] for group in craft["group"] for recipe in group["recipe"]] == ["clumsy", "fireball", "summon_water_elemental"]


def test_the_windows_and_the_mana_are_the_classic_tables_by_circle(tmp_path):
    _, _, _, destination = convert(tmp_path)

    craft = tomllib.loads((destination / "inscription.toml").read_text(encoding="utf-8"))
    by_spell = {recipe["spell"]: recipe for group in craft["group"] for recipe in group["recipe"]}

    assert (by_spell["clumsy"]["skill_min"], by_spell["clumsy"]["skill_max"], by_spell["clumsy"]["mana"]) == (-25.0, 25.0, 4)
    assert (by_spell["fireball"]["skill_min"], by_spell["fireball"]["skill_max"], by_spell["fireball"]["mana"]) == (3.5, 53.5, 9)
    water = by_spell["summon_water_elemental"]
    assert (water["skill_min"], water["skill_max"], water["mana"]) == (75.0, 125.0, 50)
    assert crafts.INSCRIPTION_SKILL[:2] == [-25.0, -10.8] and crafts.INSCRIPTION_MANA == [4, 6, 9, 11, 14, 20, 40, 50]


def test_a_recipe_takes_the_reagents_of_its_spell_by_list_and_one_blank_scroll(tmp_path):
    _, _, _, destination = convert(tmp_path)

    craft = tomllib.loads((destination / "inscription.toml").read_text(encoding="utf-8"))
    clumsy = craft["group"][0]["recipe"][0]
    lists = {entry["id"]: entry["templates"] for entry in tomllib.loads((destination / "resources.toml").read_text(encoding="utf-8"))["resource"]}

    assert clumsy["resources"] == [
        {"resource": "blood_moss", "amount": 1},
        {"resource": "0x0f88_nightshade", "amount": 1},
        {"resource": "blank_scrolls", "amount": 1},
    ]
    assert lists["blood_moss"] == ["0x0f7b_10_blood_moss", "0x0f7b_blood_moss"]
    assert lists["blank_scrolls"] == ["0x0e34_a_blank_scroll"]
    assert clumsy["skills"] == []


def test_without_the_spells_file_no_inscription_is_written(tmp_path):
    code, _, _, destination = convert(tmp_path, None)

    assert code == 0
    assert not (destination / "inscription.toml").exists()


def test_a_scroll_that_is_no_template_stops_the_conversion(tmp_path):
    code, _, error, destination = convert(tmp_path, SPELLS.replace("0x1f3e_fireball_scroll", "0x9999_nothing"))

    assert code == 2
    assert "0x9999_nothing" in error


def test_the_shipped_craft_ties_every_recipe_to_a_spell_and_its_scroll():
    root = REPOSITORY / "moongate_root"
    craft = tomllib.loads((root / "data" / "crafts" / "inscription.toml").read_text(encoding="utf-8"))
    spells = {spell["key"]: spell for spell in tomllib.loads((root / "data" / "spells.toml").read_text(encoding="utf-8"))["spell"]}
    recipes = [recipe for group in craft["group"] for recipe in group["recipe"]]

    assert len(craft["group"]) == 8
    assert sorted(recipe["spell"] for recipe in recipes) == sorted(spells)

    for recipe in recipes:
        spell = spells[recipe["spell"]]
        assert recipe["item"] == spell["scroll"]
        assert recipe["mana"] == crafts.INSCRIPTION_MANA[spell["circle"] - 1]
        assert recipe["skill_min"] == crafts.INSCRIPTION_SKILL[spell["circle"] - 1]
        assert recipe["resources"][-1] == {"resource": "blank_scrolls", "amount": 1}
        assert sum(resource["amount"] for resource in recipe["resources"][:-1]) == sum(reagent["amount"] for reagent in spell["reagents"])
