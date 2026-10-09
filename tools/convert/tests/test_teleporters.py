from __future__ import annotations

import json

import pytest

from conftest import read_toml


def entry(map_name, x, y, z, dest_map, dx, dy, dz, back=False):
    return {
        "src": {"map": map_name, "loc": [x, y, z]},
        "dst": {"map": dest_map, "loc": [dx, dy, dz]},
        "back": back,
    }


def write(tmp_path, *entries):
    source = tmp_path / "teleporters.json"
    source.write_text(json.dumps(list(entries)), encoding="utf-8")

    return source


def blocks(destination, folder):
    return read_toml(destination / folder / "teleporters.toml")["decoration"]


def test_a_teleporter_goes_to_the_folder_of_its_map_with_its_destination(tmp_path, convert):
    destination = tmp_path / "decorations"

    result = convert("modernuo-teleporters", write(tmp_path, entry("Felucca", 311, 786, -24, "Felucca", 314, 784, 0)), destination)

    assert result.code == 0
    block = blocks(destination, "felucca")[0]
    assert block["type"] == "Teleporter"
    assert block["item_id"] == 0x1BC3
    assert block["props"]["point_dest"] == [314, 784, 0]
    assert "map_dest" not in block["props"]
    assert block["locations"] == [[311, 786, -24]]
    assert "felucca/teleporters.toml: 1 teleporters in 1 blocks" in result.output


def test_every_map_has_its_folder(tmp_path, convert):
    destination = tmp_path / "decorations"
    names = ["Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "TerMur"]

    assert convert("modernuo-teleporters", write(tmp_path, *[entry(n, 1, 1, 0, n, 2, 2, 0) for n in names]), destination).code == 0

    for folder in ("felucca", "trammel", "ilshenar", "malas", "tokuno", "termur"):
        assert len(blocks(destination, folder)) == 1


def test_teleporters_with_the_same_destination_share_one_block(tmp_path, convert):
    destination = tmp_path / "decorations"
    source = write(
        tmp_path,
        entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5),
        entry("Trammel", 11, 10, 0, "Trammel", 50, 60, 5),
        entry("Trammel", 12, 10, 0, "Trammel", 51, 60, 5),
    )

    assert convert("modernuo-teleporters", source, destination).code == 0

    found = blocks(destination, "trammel")
    assert [block["locations"] for block in found] == [[[10, 10, 0], [11, 10, 0]], [[12, 10, 0]]]


def test_a_teleporter_to_another_map_names_the_map(tmp_path, convert):
    destination = tmp_path / "decorations"

    assert convert("modernuo-teleporters", write(tmp_path, entry("Malas", 10, 10, 0, "Tokuno", 50, 60, 5)), destination).code == 0

    assert blocks(destination, "malas")[0]["props"]["map_dest"] == "Tokuno"
    assert not (destination / "tokuno" / "teleporters.toml").exists()


def test_back_adds_the_return_teleporter_on_the_destinations_map(tmp_path, convert):
    destination = tmp_path / "decorations"

    assert convert("modernuo-teleporters", write(tmp_path, entry("Malas", 10, 10, 0, "Tokuno", 50, 60, 5, True)), destination).code == 0

    back = blocks(destination, "tokuno")[0]
    assert back["props"]["point_dest"] == [10, 10, 0]
    assert back["props"]["map_dest"] == "Malas"
    assert back["locations"] == [[50, 60, 5]]


@pytest.mark.parametrize(("height", "expected"), [(12, 1), (-12, 1), (13, 2)])
def test_a_later_teleporter_on_the_same_spot_replaces_the_earlier_one(tmp_path, convert, height, expected):
    """As ModernUO's [TelGen: a later teleporter replaces one on the same cell within 12 of height."""
    destination = tmp_path / "decorations"
    source = write(
        tmp_path,
        entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5),
        entry("Trammel", 10, 10, height, "Trammel", 70, 80, 0),
    )

    assert convert("modernuo-teleporters", source, destination).code == 0

    found = blocks(destination, "trammel")
    assert len(found) == expected
    assert found[-1]["props"]["point_dest"] == [70, 80, 0]


def test_running_again_replaces_the_files_and_removes_those_left_empty(tmp_path, convert):
    destination = tmp_path / "decorations"
    convert("modernuo-teleporters", write(tmp_path, entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5), entry("Malas", 1, 1, 0, "Malas", 2, 2, 0)), destination)

    convert("modernuo-teleporters", write(tmp_path, entry("Trammel", 20, 20, 0, "Trammel", 50, 60, 5)), destination)

    assert blocks(destination, "trammel")[0]["locations"] == [[20, 20, 0]]
    assert not (destination / "malas" / "teleporters.toml").exists()


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ('[{"src": {"map": "Atlantis", "loc": [1, 2, 3]}, "dst": {"map": "Trammel", "loc": [1, 2, 3]}, "back": false}]', "entry 1"),
        ('[{"src": {"map": "Trammel", "loc": [1, 2]}, "dst": {"map": "Trammel", "loc": [1, 2, 3]}, "back": false}]', "entry 1"),
        ('[{"src": {"map": "Trammel", "loc": [1, 2, 3]}, "back": false}]', "entry 1"),
        ('[{"src": {"map": "Trammel", "loc": [1, 2, 3]}, "dst": {"map": "Trammel", "loc": [1, 2, 3]}, "back": "true"}]', "entry 1"),
        ('[{"src": {"map": "Trammel", "loc": [1, "2", 3]}, "dst": {"map": "Trammel", "loc": [1, 2, 3]}, "back": false}]', "entry 1"),
        ("[]", "no teleporters"),
        ("this is not json", "not valid JSON"),
    ],
)
def test_a_bad_file_fails_naming_the_problem_and_writes_nothing(tmp_path, convert, text, expected):
    source = tmp_path / "teleporters.json"
    source.write_text(text, encoding="utf-8")
    destination = tmp_path / "decorations"

    result = convert("modernuo-teleporters", source, destination)

    assert result.code == 2
    assert expected in result.error
    assert not destination.exists()


def test_a_bad_entry_after_good_ones_keeps_the_files_of_an_earlier_run(tmp_path, convert):
    destination = tmp_path / "decorations"
    convert("modernuo-teleporters", write(tmp_path, entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5)), destination)

    result = convert(
        "modernuo-teleporters",
        write(tmp_path, entry("Trammel", 20, 20, 0, "Trammel", 50, 60, 5), entry("Atlantis", 1, 1, 0, "Trammel", 2, 2, 0)),
        destination,
    )

    assert result.code == 2
    assert "entry 2" in result.error
    assert blocks(destination, "trammel")[0]["locations"] == [[10, 10, 0]]


def test_comments_and_trailing_commas_are_read_as_modernuo_reads_them(tmp_path, convert):
    source = tmp_path / "teleporters.json"
    source.write_text("[\n// the first\n" + json.dumps(entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5)) + ",\n]", encoding="utf-8")
    destination = tmp_path / "decorations"

    assert convert("modernuo-teleporters", source, destination).code == 0

    assert len(blocks(destination, "trammel")) == 1


def test_back_on_a_spot_that_has_a_teleporter_replaces_it(tmp_path, convert):
    destination = tmp_path / "decorations"
    source = write(
        tmp_path,
        entry("Trammel", 50, 60, 5, "Trammel", 1, 1, 0),
        entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5, True),
    )

    assert convert("modernuo-teleporters", source, destination).code == 0

    found = blocks(destination, "trammel")
    assert len(found) == 2
    back = next(block for block in found if block["locations"][0][0] == 50)
    assert back["props"]["point_dest"] == [10, 10, 0]


def test_a_missing_file_fails(tmp_path, convert):
    result = convert("modernuo-teleporters", tmp_path / "teleporters.json", tmp_path / "decorations")

    assert result.code == 2
    assert "does not exist" in result.error
