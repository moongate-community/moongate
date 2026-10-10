"""The ``uox-spells`` command: UOX3's spells.dfn into data/spells.toml."""

from __future__ import annotations

import io
import tomllib
from pathlib import Path

import pytest

from moongate_convert import spells
from moongate_convert.cli import main

REPOSITORY = Path(__file__).resolve().parents[3]
UOX_SPELLS = Path.home() / "projects" / "others" / "UOX3" / "data" / "dfndata" / "spells" / "spells.dfn"


def spell_block(number: int, name: str, extra: str = "") -> str:
    return f"[SPELL {number}]\n{{\nNAME={name}\nENABLE=1\nCIRCLE=1\nMANA=4\nMANTRA=Uus Jux\nACTION=17\n{extra}}}\n"


def scrolls_folder(tmp_path: Path) -> Path:
    items = tmp_path / "items"
    items.mkdir()
    entries = []

    for graphic in range(0x1F2D, 0x1F2D + spells.SPELL_COUNT):
        entries.append(f'[[item]]\nid = "0x{graphic:04x}_x_scroll"\nitem_id = {graphic}\n')
        entries.append(f'[[item]]\nid = "alias{graphic}"\nbase_id = "0x{graphic:04x}_x_scroll"\nitem_id = 0\n')

    entries.append('[[item]]\nid = "0x0f7a_black_pearl"\n[[item]]\nid = "0x0f7b_blood_moss"\n[[item]]\nid = "0x0f88_nightshade"\n')
    entries.append('[[item]]\nid = "0x0f8c_sulfurous_ash"\n[[item]]\nid = "0x0f84_garlic"\n[[item]]\nid = "0x0f85_ginseng"\n')
    entries.append('[[item]]\nid = "0x0f86_mandrake_root"\n[[item]]\nid = "0x0f8d_spider_silk"\n')
    (items / "magic.toml").write_text("\n".join(entries), encoding="utf-8")

    return items


def full_source(tmp_path: Path, overrides: dict[int, str] | None = None) -> Path:
    blocks = [spell_block(number, f"Spell {number}") for number in range(1, spells.SPELL_COUNT + 1)]

    for number, text in (overrides or {}).items():
        blocks[number - 1] = text

    source = tmp_path / "spells.dfn"
    source.write_text("\n".join(blocks), encoding="utf-8")

    return source


def convert(tmp_path: Path, source: Path) -> tuple[int, str, str, dict]:
    out, err = io.StringIO(), io.StringIO()
    destination = tmp_path / "data"
    code = main(
        ["uox-spells", "--source", str(source), "--items", str(scrolls_folder(tmp_path)), "--destination", str(destination)], out, err
    )
    written = destination / "spells.toml"
    data = tomllib.loads(written.read_text(encoding="utf-8")) if written.is_file() else {}

    return code, out.getvalue(), err.getvalue(), data


def test_a_spell_keeps_what_the_engine_reads(tmp_path: Path):
    clumsy = spell_block(
        1,
        "Clumsy",
        "MOSS=1\nSHADE=1\nTARG=Select target for clumsy.\nFLAGS=0x01C9\nSTATFX=37 79 00 0F\nSOUNDFX=0x01DF\n",
    )
    code, _, error, data = convert(tmp_path, full_source(tmp_path, {1: clumsy}))

    assert code == 0, error
    spell = data["spell"][0]
    assert spell["id"] == 1
    assert spell["key"] == "clumsy"
    assert spell["name"] == "Clumsy"
    assert spell["circle"] == 1
    assert spell["mantra"] == "Uus Jux"
    assert spell["action"] == 17
    assert spell["reagents"] == [
        {"template": "0x0f7b_blood_moss", "amount": 1},
        {"template": "0x0f88_nightshade", "amount": 1},
    ]
    assert spell["target"] == "mobile"
    assert spell["harmful"] is True
    assert spell["resistable"] is True
    assert spell["reflectable"] is True
    assert spell["sound"] == 0x1DF
    assert spell["effect"] == 0x3779
    assert spell["effect_duration"] == 15
    assert spell["prompt"] == "Select target for clumsy."
    assert spell["scroll"] == "0x1f2e_x_scroll"
    assert spell["enabled"] is True


def test_all_64_spells_come_out_in_order(tmp_path: Path):
    code, output, _, data = convert(tmp_path, full_source(tmp_path))

    assert code == 0
    assert "64 spell(s)" in output
    assert [spell["id"] for spell in data["spell"]] == list(range(1, 65))
    assert len({spell["key"] for spell in data["spell"]}) == 64


def test_the_scroll_of_reactive_armor_comes_before_the_first_circle(tmp_path: Path):
    _, _, _, data = convert(tmp_path, full_source(tmp_path))

    scrolls = {spell["id"]: spell["scroll"] for spell in data["spell"]}
    assert scrolls[7] == "0x1f2d_x_scroll"
    assert scrolls[1] == "0x1f2e_x_scroll"
    assert scrolls[8] == "0x1f34_x_scroll"
    assert scrolls[64] == "0x1f6c_x_scroll"


@pytest.mark.parametrize(
    ("flags", "spell_id", "kind"),
    [("0x0000", 2, "none"), ("0x0009", 4, "mobile"), ("0x0003", 13, "item"), ("0x01A5", 28, "location"), ("0x0011", 32, "item")],
)
def test_the_target_kind_follows_the_flags(tmp_path: Path, flags: str, spell_id: int, kind: str):
    block = spell_block(spell_id, "Some", f"FLAGS={flags}\nTARG=Pick\n")
    _, _, _, data = convert(tmp_path, full_source(tmp_path, {spell_id: block}))

    assert data["spell"][spell_id - 1]["target"] == kind


def test_a_spell_with_no_target_has_no_prompt(tmp_path: Path):
    block = spell_block(2, "Create Food", "FLAGS=0x0000\nTARG=Not used\n")
    _, _, _, data = convert(tmp_path, full_source(tmp_path, {2: block}))

    assert data["spell"][1]["prompt"] == ""


def test_the_british_names_are_the_clients(tmp_path: Path):
    block = spell_block(7, "Reactive Armour", "FLAGS=0x0009\n")
    _, _, _, data = convert(tmp_path, full_source(tmp_path, {7: block}))

    assert data["spell"][6]["key"] == "reactive_armor"
    assert data["spell"][6]["name"] == "Reactive Armor"


def test_the_words_UOX3_misspells_are_the_classic_ones(tmp_path: Path):
    food = spell_block(2, "Create Food", "MANTRA=In Mani Yelm\n")
    strike = spell_block(51, "Flamestrike", "MANTRA=Kal Vas Vlam\n")
    _, _, _, data = convert(tmp_path, full_source(tmp_path, {2: food, 51: strike}))

    assert data["spell"][1]["mantra"] == "In Mani Ylem"
    assert data["spell"][50]["mantra"] == "Kal Vas Flam"
    assert data["spell"][0]["mantra"] == "Uus Jux"


def test_a_missing_spell_stops_the_run(tmp_path: Path):
    source = tmp_path / "spells.dfn"
    source.write_text(spell_block(1, "Clumsy"), encoding="utf-8")

    code, _, error, _ = convert(tmp_path, source)

    assert code == 2
    assert "are not in spells.dfn" in error


def test_a_reagent_with_no_template_stops_the_run(tmp_path: Path):
    block = spell_block(1, "Clumsy", "MOSS=1\n")
    source = full_source(tmp_path, {1: block})
    items = scrolls_folder(tmp_path)
    (items / "magic.toml").write_text(
        "\n".join(f'[[item]]\nid = "0x{g:04x}_x_scroll"\nitem_id = {g}\n' for g in range(0x1F2D, 0x1F2D + 64)), encoding="utf-8"
    )
    out, err = io.StringIO(), io.StringIO()

    code = main(["uox-spells", "--source", str(source), "--items", str(items), "--destination", str(tmp_path / "data")], out, err)

    assert code == 2
    assert "0x0f7b_blood_moss" in err.getvalue()


def test_a_folder_without_the_file_is_an_error(tmp_path: Path):
    empty = tmp_path / "empty"
    empty.mkdir()

    code, _, error, _ = convert(tmp_path, empty)

    assert code == 2
    assert "spells.dfn is missing" in error


@pytest.mark.skipif(not UOX_SPELLS.is_file(), reason="the UOX3 sources are not checked out")
def test_the_real_data_converts_and_is_what_the_server_ships(tmp_path: Path):
    items = REPOSITORY / "moongate_root" / "templates" / "items"
    out, err = io.StringIO(), io.StringIO()

    code = main(["uox-spells", "--source", str(UOX_SPELLS), "--items", str(items), "--destination", str(tmp_path)], out, err)

    assert code == 0, err.getvalue()
    shipped = (REPOSITORY / "moongate_root" / "data" / "spells.toml").read_text(encoding="utf-8")
    assert (tmp_path / "spells.toml").read_text(encoding="utf-8") == shipped
