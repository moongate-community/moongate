from __future__ import annotations

import json

from conftest import read_toml


def write(source, path, text):
    file = source / path
    file.parent.mkdir(parents=True, exist_ok=True)
    file.write_text(text, encoding="utf-8")


def spawner(location, count, entries, **more):
    return {"location": location, "count": count, "minDelay": "00:05:00", "maxDelay": "00:10:00", "entries": entries, **more}


def chest(name, max_count=None):
    return {"name": name} if max_count is None else {"name": name, "maxCount": max_count, "probability": 100}


def spawns(destination, folder):
    return read_toml(destination / folder / "treasure_chests.toml")["spawn"]


def test_a_chest_spawner_becomes_a_region_of_items_around_its_spot(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    write(source, "shared/felucca/Shame.json", json.dumps([spawner([5400, 20, 10], 1, [chest("TreasureChestLevel3", 1)], homeRange=2)]))

    result = convert("modernuo-chests", source, destination)

    assert result.code == 0
    spawn = spawns(destination, "felucca")[0]
    assert spawn["id"] == "felucca_chest_shared_shame_0"
    assert spawn["item_ids"] == ["treasure_chest_level_3"]
    assert spawn["mobile_ids"] == []
    assert (spawn["max"], spawn["call"], spawn["min_minutes"], spawn["max_minutes"]) == (1, 1, 5, 10)
    assert spawn["areas"] == [{"x1": 5398, "y1": 18, "x2": 5402, "y2": 22}]
    # Under the spawner's ceiling, so a chest of a cave is not put on the hill over it.
    assert spawn["z"] == 26
    assert spawn["name"] == "Treasure chest level 3"
    assert "1 chest region(s)" in result.output


def test_two_levels_with_one_place_give_one_chest_at_a_time(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    write(source, "shared/ilshenar/Ratmancave.json", json.dumps([spawner([1, 2, 0], 1, [chest("TreasureChestLevel3", 1), chest("TreasureChestLevel4", 1)])]))

    assert convert("modernuo-chests", source, destination).code == 0

    spawn = spawns(destination, "ilshenar")[0]
    assert spawn["max"] == 1
    assert spawn["item_ids"] == ["treasure_chest_level_3", "treasure_chest_level_4"]


def test_a_spawner_of_chests_and_creatures_gives_one_region_of_its_chests_only(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    write(
        source,
        "shared/trammel/Deceit.json",
        json.dumps(
            [
                spawner([100, 200, 0], 3, [chest("Lich", 3), chest("TreasureChestLevel1", 1), chest("TreasureChestLevel2", 5)], homeRange=0),
                spawner([1, 2, 0], 1, [chest("Orc", 1)]),
            ]
        ),
    )

    assert convert("modernuo-chests", source, destination).code == 0

    found = spawns(destination, "trammel")
    assert len(found) == 1
    spawn = found[0]
    assert spawn["id"] == "trammel_chest_shared_deceit_0"
    assert spawn["item_ids"] == ["treasure_chest_level_1", "treasure_chest_level_2"]
    assert spawn["name"] == "Treasure chest level 1, 2"
    # The caps of the chests together, the spawner's count at most.
    assert spawn["max"] == 3
    assert spawn["areas"][0] == {"x1": 100, "y1": 200, "x2": 100, "y2": 200}


def test_every_map_and_era_go_into_one_file_per_map_and_a_previous_file_is_replaced(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    text = json.dumps([spawner([1, 2, 0], 1, [chest("TreasureChestLevel4")])])
    write(source, "shared/felucca/A.json", text)
    write(source, "post-uoml/felucca/B.json", text)
    write(source, "shared/ilshenar/C.json", text)
    # An era of an old client is not read.
    write(source, "uoml/felucca/D.json", text)
    write(destination, "felucca/treasure_chests.toml", '[[spawn]]\nid = "old"\n')
    write(destination, "felucca/dungeon_shame.toml", "# kept\n")

    assert convert("modernuo-chests", source, destination).code == 0

    assert sorted(spawn["id"] for spawn in spawns(destination, "felucca")) == ["felucca_chest_post_uoml_b_0", "felucca_chest_shared_a_0"]
    assert len(spawns(destination, "ilshenar")) == 1
    assert (destination / "felucca" / "dungeon_shame.toml").read_text() == "# kept\n"


def test_the_chests_of_a_spawner_share_its_count(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    entries = [chest("TreasureChestLevel2", 1), chest("TreasureChestLevel2", 2), chest("TreasureChestLevel3", 4), chest("TreasureChestLevel3", 4)]
    write(source, "shared/felucca/A.json", json.dumps([spawner([1, 2, 0], 5, entries)]))

    assert convert("modernuo-chests", source, destination).code == 0

    spawn = spawns(destination, "felucca")[0]
    # A level listed twice is one item; the caps add up to 11, the spawner allows 5.
    assert spawn["item_ids"] == ["treasure_chest_level_2", "treasure_chest_level_3"]
    assert spawn["max"] == 5


def test_a_chest_of_a_level_without_a_template_is_counted_and_left_out(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    write(source, "shared/felucca/A.json", json.dumps([spawner([1, 2, 0], 1, [chest("TreasureChestLevel9"), chest("treasurechestlevel2")])]))

    result = convert("modernuo-chests", source, destination)

    assert result.code == 0
    assert spawns(destination, "felucca")[0]["item_ids"] == ["treasure_chest_level_2"]
    assert "TreasureChestLevel9" in result.output


def test_no_chest_anywhere_fails_and_writes_nothing(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    write(source, "shared/felucca/A.json", json.dumps([{"location": [1, 2, 0], "count": 1, "entries": [{"name": "Orc"}]}]))

    result = convert("modernuo-chests", source, destination)

    assert result.code == 2
    assert "no treasure chest" in result.error
    assert not destination.exists()


def test_a_missing_folder_fails(tmp_path, convert):
    result = convert("modernuo-chests", tmp_path / "Spawns", tmp_path / "spawns")

    assert result.code == 2
    assert "does not exist" in result.error


def test_a_chest_on_another_map_than_its_folder_keeps_its_own_map(tmp_path, convert):
    """As the chests of the Fan Dancer's Dojo: in ModernUO's tokuno folder, on the Malas map."""
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    write(
        source,
        "shared/tokuno/FanDancersDojo.json",
        json.dumps(
            [
                spawner([80, 700, 10], 1, [chest("TreasureChestLevel2", 1)], map="Malas", homeRange=2),
                spawner([800, 700, 10], 1, [chest("TreasureChestLevel2", 1)], homeRange=2),
            ]
        ),
    )

    assert convert("modernuo-chests", source, destination).code == 0

    # The file and the ids stay with the folder; a spawner that names no map takes the folder's.
    assert [(spawn["id"], spawn["map"]) for spawn in spawns(destination, "tokuno")] == [
        ("tokuno_chest_shared_fan_dancers_dojo_0", "malas"),
        ("tokuno_chest_shared_fan_dancers_dojo_1", "tokuno"),
    ]


def test_a_file_that_is_not_json_fails_naming_it(tmp_path, convert):
    source, destination = tmp_path / "Spawns", tmp_path / "spawns"
    write(source, "shared/felucca/A.json", "this is not json")

    result = convert("modernuo-chests", source, destination)

    assert result.code == 2
    assert "A.json" in result.error
