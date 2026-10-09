from __future__ import annotations

from conftest import read_toml


def write(tmp_path, lines):
    source = tmp_path / "signs.cfg"
    source.write_text("\n".join(lines) + "\n", encoding="utf-8")

    return source


def blocks(destination, folder):
    name = "_signs.toml" if folder == "trammel" else "signs.toml"

    return read_toml(destination / folder / name)["decoration"]


def test_signs_of_every_facet_go_to_the_folder_of_their_maps(tmp_path, convert):
    source = write(
        tmp_path,
        [
            "0 3032 373 904 -1 #1016093",
            "1 2996 10 20 0 #1016315",
            "2 2979 3632 2537 0 The Shakin' Bakery",
            "3 3016 30 40 5 #1016095",
            "4 3026 50 60 0 #1016082",
            "5 3025 70 80 4 #1016216",
        ],
    )
    destination = tmp_path / "decorations"

    assert convert("modernuo-signs", source, destination).code == 0

    britannia = blocks(destination, "britannia")[0]
    assert britannia["type"] == "LocalizedSign"
    assert britannia["item_id"] == 3032
    assert britannia["props"]["label_number"] == 1016093
    assert britannia["locations"] == [[373, 904, -1]]
    # The signs of Trammel alone are those of the old Haven, a ruin on the client's map: written set aside.
    assert not (destination / "trammel" / "signs.toml").exists()
    trammel = blocks(destination, "trammel")[0]
    assert trammel["type"] == "Sign"
    assert trammel["props"]["name"] == "The Shakin' Bakery"
    for folder in ("felucca", "ilshenar", "malas", "tokuno"):
        assert len(blocks(destination, folder)) == 1


def test_signs_with_the_same_graphic_and_text_share_one_block(tmp_path, convert):
    source = write(tmp_path, ["0 3032 1 2 3 #1016093", "0 3032 4 5 6 #1016093", "0 3033 7 8 9 #1016093"])
    destination = tmp_path / "decorations"

    assert convert("modernuo-signs", source, destination).code == 0

    found = blocks(destination, "britannia")
    assert len(found) == 2
    assert len(found[0]["locations"]) == 2


def test_a_sign_in_the_malas_towns_gets_the_hue_of_its_town(tmp_path, convert):
    source = write(tmp_path, ["4 3026 970 510 0 #1", "4 3026 1960 1278 0 #1", "4 3026 10 10 0 #1"])
    destination = tmp_path / "decorations"

    assert convert("modernuo-signs", source, destination).code == 0

    found = blocks(destination, "malas")
    assert len(found) == 3
    assert found[0]["props"]["hue"] == 0x47E
    assert found[1]["props"]["hue"] == 0x44E
    assert "hue" not in found[2]["props"]


def test_a_name_with_quotes_is_escaped(tmp_path, convert):
    source = write(tmp_path, ['0 3032 1 2 3 The "Best" \\ Inn'])
    destination = tmp_path / "decorations"

    assert convert("modernuo-signs", source, destination).code == 0

    assert blocks(destination, "britannia")[0]["props"]["name"] == 'The "Best" \\ Inn'


def test_a_missing_source_fails(tmp_path, convert):
    result = convert("modernuo-signs", tmp_path / "signs.cfg", tmp_path / "decorations")

    assert result.code == 2
    assert "signs.cfg" in result.error


def test_a_line_that_is_not_a_sign_fails_naming_it_and_writes_nothing(tmp_path, convert):
    source = write(tmp_path, ["0 3032 1 2 3 #1", "9 3032 1 2 3 #1"])
    destination = tmp_path / "decorations"

    result = convert("modernuo-signs", source, destination)

    assert result.code == 2
    assert "line 2" in result.error
    assert not destination.exists()


def test_a_second_run_removes_the_file_of_a_facet_left_empty(tmp_path, convert):
    destination = tmp_path / "decorations"
    convert("modernuo-signs", write(tmp_path, ["1 2996 10 20 0 #1", "4 3026 50 60 0 #1"]), destination)

    convert("modernuo-signs", write(tmp_path, ["1 2996 10 20 0 #1"]), destination)

    assert (destination / "felucca" / "signs.toml").exists()
    assert not (destination / "malas" / "signs.toml").exists()
