from __future__ import annotations

import pytest

from conftest import read_toml


def write(source, map_name, text):
    source.mkdir(exist_ok=True)
    (source / f"{map_name}.json").write_text(text, encoding="utf-8")


def places(destination):
    return read_toml(destination)["location"]


def row(place):
    return place["map"], place["category"], place["name"], place["location"]


def test_a_place_in_nested_categories_keeps_its_map_its_category_path_and_its_spot(tmp_path, convert):
    source, destination = tmp_path / "Locations", tmp_path / "locations.toml"
    write(
        source,
        "felucca",
        """{ "name": "Felucca", "categories": [
             { "name": "Dungeons", "categories": [
               { "name": "Covetous", "locations": [
                 { "name": "Entrance", "location": [2499, 919, 0] },
                 { "name": "Level 1", "location": [5456, 1863, 0] } ] } ] } ] }""",
    )

    result = convert("modernuo-locations", source, destination)

    assert result.code == 0
    assert [row(place) for place in places(destination)] == [
        ("felucca", "Dungeons/Covetous", "Entrance", "(2499, 919, 0)"),
        ("felucca", "Dungeons/Covetous", "Level 1", "(5456, 1863, 0)"),
    ]
    assert "2 places on 1 maps" in result.output


def test_places_beside_categories_and_at_the_top_of_a_map_are_kept(tmp_path, convert):
    source, destination = tmp_path / "Locations", tmp_path / "locations.toml"
    write(
        source,
        "malas",
        """{ "name": "Malas", "locations": [ { "name": "Arena", "location": [1, 2, -3] } ],
             "categories": [ { "name": "Towns",
               "locations": [ { "name": "Luna", "location": [989, 520, -50] } ],
               "categories": [ { "name": "Inns", "locations": [ { "name": "Luna Inn", "location": [4, 5, 6] } ] } ] } ] }""",
    )

    assert convert("modernuo-locations", source, destination).code == 0

    assert [row(place) for place in places(destination)] == [
        ("malas", "", "Arena", "(1, 2, -3)"),
        ("malas", "Towns", "Luna", "(989, 520, -50)"),
        ("malas", "Towns/Inns", "Luna Inn", "(4, 5, 6)"),
    ]


def test_every_map_file_goes_into_the_one_file_in_the_order_of_the_maps(tmp_path, convert):
    source, destination = tmp_path / "Locations", tmp_path / "locations.toml"

    for map_name in ("trammel", "felucca", "termur", "tokuno", "malas", "ilshenar"):
        write(source, map_name, '{ "name": "X", "locations": [ { "name": "Here", "location": [1, 2, 3] } ] }')

    assert convert("modernuo-locations", source, destination).code == 0

    assert [place["map"] for place in places(destination)] == ["felucca", "trammel", "ilshenar", "malas", "tokuno", "termur"]


def test_a_name_with_a_quote_or_a_slash_is_written_so_it_reads_back(tmp_path, convert):
    source, destination = tmp_path / "Locations", tmp_path / "locations.toml"
    write(
        source,
        "felucca",
        '{ "name": "F", "categories": [ { "name": "East/West", "locations": [ { "name": "Buccaneer\'s \\"Den\\"", "location": [1, 2, 3] } ] } ] }',
    )

    assert convert("modernuo-locations", source, destination).code == 0

    place = places(destination)[0]
    # A slash would split the category in two.
    assert (place["category"], place["name"]) == ("East-West", 'Buccaneer\'s "Den"')


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ('{ "name": "F", "locations": [ { "name": "Here", "location": [1, 2] } ] }', "Here"),
        ('{ "name": "F", "locations": [ { "location": [1, 2, 3] } ] }', "felucca.json"),
        ('{ "name": "F", "locations": [ { "name": "Here", "location": [1, "2", 3] } ] }', "Here"),
        ("this is not json", "felucca.json"),
    ],
)
def test_a_bad_file_fails_naming_it_and_keeps_the_file_of_an_earlier_run(tmp_path, convert, text, expected):
    source, destination = tmp_path / "Locations", tmp_path / "locations.toml"
    write(source, "felucca", '{ "name": "F", "locations": [ { "name": "Kept", "location": [1, 2, 3] } ] }')
    assert convert("modernuo-locations", source, destination).code == 0
    write(source, "felucca", text)

    result = convert("modernuo-locations", source, destination)

    assert result.code == 2
    assert expected in result.error
    assert places(destination)[0]["name"] == "Kept"


def test_a_folder_with_no_map_file_fails(tmp_path, convert):
    source, destination = tmp_path / "Locations", tmp_path / "locations.toml"
    source.mkdir()

    result = convert("modernuo-locations", source, destination)

    assert result.code == 2
    assert "no places" in result.error
    assert not destination.exists()


def test_a_missing_folder_fails(tmp_path, convert):
    result = convert("modernuo-locations", tmp_path / "Locations", tmp_path / "locations.toml")

    assert result.code == 2
    assert "does not exist" in result.error


def test_cell_seven_of_the_jail_is_moved_off_the_spot_of_cell_six(tmp_path, convert):
    source, destination = tmp_path / "Locations", tmp_path / "locations.toml"
    write(
        source,
        "felucca",
        '{ "name": "F", "categories": [ { "name": "Internal", "categories": [ { "name": "Jail Cells", "locations": ['
        ' { "name": "Cell 6", "location": [5276, 1174, 0] }, { "name": "Cell 7", "location": [5286, 1174, 0] },'
        ' { "name": "Cell 8", "location": [5306, 1174, 0] } ] } ] } ] }',
    )

    result = convert("modernuo-locations", source, destination)

    assert result.code == 0
    assert [place["location"] for place in places(destination)] == ["(5276, 1174, 0)", "(5296, 1174, 0)", "(5306, 1174, 0)"]
    assert "Cell 7" in result.output
