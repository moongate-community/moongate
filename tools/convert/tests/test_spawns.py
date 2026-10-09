"""The ModernUO spawn converter, ported from the C# ``ModernUoSpawnConverterTests``."""

from __future__ import annotations

import io
import json
from pathlib import Path

import pytest

from moongate_convert import spawns
from moongate_convert.cli import main

from conftest import read_toml


def spawner(x, y, z, home_range, count, min_delay, max_delay, *names, map_name="Malas") -> dict:
    return {
        "$type": "Spawner",
        "name": "Spawner (1)",
        "location": [x, y, z],
        "map": map_name,
        "count": count,
        "minDelay": min_delay,
        "maxDelay": max_delay,
        "team": 0,
        "homeRange": home_range,
        "walkingRange": 2,
        "entries": [{"name": name, "maxCount": count, "probability": 100} for name in names],
    }


class Env:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.source = root / "Spawns"
        self.mobiles = root / "mobiles"
        self.destination = root / "spawns"
        self.output = io.StringIO()
        self.error = io.StringIO()
        self.mobiles.mkdir(parents=True)
        (self.mobiles / "monsters.toml").write_text(
            "".join(f'[[mobile]]\nid = "{name}"\n' for name in ["great_hart", "dullcopperele", "banker", "orcmage", "earthele"]),
            encoding="utf-8",
        )

    def write(self, relative: str, *spawners: dict) -> None:
        path = self.source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(list(spawners)), encoding="utf-8")

    def run(self, *maps: str, only: str | None = None, mobiles: Path | None = None) -> int:
        return spawns.run(self.source, list(maps), mobiles or self.mobiles, self.destination, self.output, self.error, only)

    def read(self, folder: str, file: str) -> list[dict]:
        return read_toml(self.destination / folder / f"{file}.toml")["spawn"]


@pytest.fixture
def env(tmp_path) -> Env:
    return Env(tmp_path)


def test_the_spawners_of_a_map_become_spawn_regions_with_their_mobiles_resolved(env):
    env.write(
        "shared/malas/Outdoors.json",
        spawner(1000, 500, 10, 5, 3, "00:05:00", "00:10:00", "GreatHart", "DullCopperElemental"),
        spawner(20, 30, -5, 0, 2, "00:00:30", "00:01:00", "Minter", "OrcishMage", "EarthElemental"),
    )

    assert env.run("Malas") == 0, env.error.getvalue()

    regions = env.read("malas", "modernuo_outdoors")
    assert [region["id"] for region in regions] == ["malas_modernuo_shared_outdoors_0", "malas_modernuo_shared_outdoors_1"]

    hart = regions[0]
    assert hart["map"] == "malas"
    assert hart["mobile_ids"] == ["great_hart", "dullcopperele"]
    assert (hart["max"], hart["min_minutes"], hart["max_minutes"], hart["call"]) == (3, 5, 10, 1)
    assert [(a["x1"], a["y1"], a["x2"], a["y2"]) for a in hart["areas"]] == [(995, 495, 1005, 505)]
    assert hart["z"] == 26

    # A spawner without a home range spawns on its spot, as in ModernUO; a short delay is a minute at least.
    bank = regions[1]
    assert bank["mobile_ids"] == ["banker", "orcmage", "earthele"]
    assert (bank["min_minutes"], bank["max_minutes"]) == (1, 1)
    assert (bank["areas"][0]["x1"], bank["areas"][0]["y1"], bank["areas"][0]["x2"], bank["areas"][0]["y2"]) == (20, 30, 20, 30)


def test_a_spawner_on_another_map_than_its_folder_keeps_its_own_map(env):
    # As the Yomotsu Mines and the Fan Dancer's Dojo: in ModernUO's tokuno folder, on the Malas map.
    second = spawner(700, 1200, 25, 5, 2, "00:05:00", "00:10:00", "GreatHart", map_name="Tokuno")
    third = spawner(701, 1201, 25, 5, 2, "00:05:00", "00:10:00", "GreatHart")
    del third["map"]
    env.write(
        "shared/tokuno/YomutsoMines.json", spawner(100, 80, 0, 5, 2, "00:05:00", "00:10:00", "EarthElemental"), second, third
    )

    assert env.run("Tokuno") == 0, env.error.getvalue()

    # The ids stay with the source folder; each region goes into the folder of its map, where the server reads the map from.
    # A spawner that names no map takes the folder's.
    assert [(r["id"], r["map"], r["name"]) for r in env.read("malas", "modernuo_yomutso_mines")] == [
        ("tokuno_modernuo_shared_yomutso_mines_0", "malas", "Malas earthele")
    ]
    assert [(r["id"], r["map"], r["name"]) for r in env.read("tokuno", "modernuo_yomutso_mines")] == [
        ("tokuno_modernuo_shared_yomutso_mines_1", "tokuno", "Tokuno great_hart"),
        ("tokuno_modernuo_shared_yomutso_mines_2", "tokuno", "Tokuno great_hart"),
    ]


@pytest.mark.parametrize("order", [("Tokuno", "Malas"), ("Malas", "Tokuno")])
def test_a_region_moved_to_another_converted_map_survives_its_old_files_whatever_the_order(env, order):
    env.write("shared/tokuno/YomutsoMines.json", spawner(100, 80, 0, 5, 2, "00:05:00", "00:10:00", "EarthElemental"))
    env.write("shared/malas/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    (env.destination / "malas").mkdir(parents=True)
    (env.destination / "malas" / "modernuo_gone.toml").write_text("")

    assert env.run(*order) == 0, env.error.getvalue()

    assert len(env.read("malas", "modernuo_yomutso_mines")) == 1
    assert len(env.read("malas", "modernuo_vendors")) == 1
    assert not (env.destination / "malas" / "modernuo_gone.toml").exists()
    assert not (env.destination / "tokuno" / "modernuo_yomutso_mines.toml").exists()


def test_an_entry_capped_below_the_count_gets_its_own_region_and_unknown_entries_keep_their_share(env):
    south = spawner(500, 500, 0, 10, 10, "00:05:00", "00:10:00", "GreatHart", "Slith")
    south["entries"].insert(0, {"name": "Minter", "maxCount": 1, "probability": 100})
    env.write("post-uoml/malas/South.json", south)

    assert env.run("Malas") == 0, env.error.getvalue()

    # ModernUO: at most one banker; the other 9 picks split between the hart and the unknown slith.
    regions = env.read("malas", "modernuo_south")
    assert [(r["id"], r["mobile_ids"], r["max"]) for r in regions] == [
        ("malas_modernuo_post_uoml_south_0", ["great_hart"], 5),
        ("malas_modernuo_post_uoml_south_0_banker", ["banker"], 1),
    ]


def test_a_capped_class_listed_twice_is_one_region_with_both_caps(env):
    bedlam = spawner(500, 500, 0, 10, 10, "00:05:00", "00:10:00", "GreatHart")
    twice = {"name": "Minter", "maxCount": 1, "probability": 100}
    bedlam["entries"][:0] = [twice, dict(twice)]
    env.write("shared/malas/Bedlam.json", bedlam)

    assert env.run("Malas") == 0, env.error.getvalue()

    assert [(r["id"], r["max"]) for r in env.read("malas", "modernuo_bedlam")] == [
        ("malas_modernuo_shared_bedlam_0", 8),
        ("malas_modernuo_shared_bedlam_0_banker", 2),
    ]


def test_spawn_bounds_are_the_area_and_their_top_the_ceiling(env):
    towns = spawner(713, 1351, 25, 0, 4, "00:05:00", "00:10:00", "GreatHart", map_name="Tokuno")
    towns["spawnBounds"] = {"start": {"x": 693, "y": 1331, "z": -128}, "end": {"x": 733, "y": 1371, "z": 40}}
    env.write("shared/tokuno/TownsLife.json", towns)

    assert env.run("Tokuno") == 0, env.error.getvalue()

    [region] = env.read("tokuno", "modernuo_towns_life")
    area = region["areas"][0]
    assert (area["x1"], area["y1"], area["x2"], area["y2"], region["z"]) == (693, 1331, 733, 1371, 40)


def test_a_mobiles_folder_without_templates_is_an_error_and_keeps_the_old_files(env):
    env.write("shared/malas/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    (env.destination / "malas").mkdir(parents=True)
    (env.destination / "malas" / "modernuo_vendors.toml").write_text("")

    assert env.run("Malas", mobiles=env.root / "nowhere") == 2

    assert "no mobile templates" in env.error.getvalue()
    assert (env.destination / "malas" / "modernuo_vendors.toml").exists()


def test_unknown_mobiles_are_reported_and_a_spawner_with_none_known_is_skipped(env):
    env.write(
        "post-uoml/termur/Outdoors.json",
        spawner(100, 100, 0, 5, 2, "00:05:00", "00:10:00", "Slith", "GreatHart", map_name="TerMur"),
        spawner(200, 200, 0, 5, 2, "00:05:00", "00:10:00", "Slith", map_name="TerMur"),
    )

    assert env.run("TerMur") == 0, env.error.getvalue()

    [region] = env.read("termur", "modernuo_outdoors")
    assert region["mobile_ids"] == ["great_hart"]
    assert region["map"] == "ter_mur"
    assert "unknown mobile Slith" in env.output.getvalue()
    assert "spawner without known mobiles skipped" in env.output.getvalue()


def test_it_reads_shared_and_post_uoml_of_the_chosen_maps_only_and_replaces_its_own_old_files(env):
    env.write("shared/malas/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    env.write("post-uoml/malas/Vendors.json", spawner(40, 40, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    env.write("uoml/malas/Vendors.json", spawner(70, 70, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    env.write("shared/tokuno/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    (env.destination / "malas").mkdir(parents=True)
    (env.destination / "malas" / "modernuo_gone.toml").write_text("")
    (env.destination / "malas" / "town_luna.toml").write_text("")

    assert env.run("Malas") == 0, env.error.getvalue()

    assert [region["areas"][0]["x1"] + 2 for region in env.read("malas", "modernuo_vendors")] == [10, 40]
    assert not (env.destination / "malas" / "modernuo_gone.toml").exists()
    assert (env.destination / "malas" / "town_luna.toml").exists()
    assert not (env.destination / "tokuno").exists()


def test_a_missing_source_is_an_error(env):
    assert env.run("Malas") == 2
    assert "does not exist" in env.error.getvalue()


def test_a_class_that_is_an_npc_list_becomes_a_list_of_the_region_and_a_mobile_of_the_same_name_stays_a_mobile(env):
    (env.root / "npc_lists").mkdir()
    (env.root / "npc_lists" / "lists.toml").write_text('[[npc_list]]\nid = "mageguildmaster"\n[[npc_list]]\nid = "banker"\n')
    env.write("shared/malas/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "MageGuildmaster", "Minter"))

    assert env.run("Malas") == 0, env.error.getvalue()

    [region] = env.read("malas", "modernuo_vendors")
    assert region["npc_list_ids"] == ["mageguildmaster"]
    assert region["mobile_ids"] == ["banker"]


def test_with_only_it_converts_the_entries_of_those_classes_into_one_file_and_keeps_the_other_files(env):
    (env.mobiles / "guild.toml").write_text('[[mobile]]\nid = "m_mage_guildmaster"\n')
    (env.root / "npc_lists").mkdir()
    (env.root / "npc_lists" / "lists.toml").write_text('[[npc_list]]\nid = "mageguildmaster"\n')
    env.write("shared/malas/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "MageGuildmaster", "Minter"))
    (env.destination / "malas").mkdir(parents=True)
    (env.destination / "malas" / "modernuo_other.toml").write_text('[[spawn]]\nid = "keep"\n')

    assert env.run("Malas", only="Guildmaster") == 0, env.error.getvalue()

    [region] = env.read("malas", "modernuo_guildmasters")
    assert region["npc_list_ids"] == ["mageguildmaster"]
    assert region["mobile_ids"] == []
    assert (env.destination / "malas" / "modernuo_other.toml").exists()
    assert not (env.destination / "malas" / "modernuo_vendors.toml").exists()


def test_a_spawner_file_that_is_not_what_modernuo_writes_is_an_error_and_writes_nothing(env):
    env.write("shared/malas/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    (env.source / "shared" / "malas" / "Broken.json").write_text('[{"location": [1, 2]}]')

    assert env.run("Malas") == 2

    assert "is not a spawner" in env.error.getvalue()
    assert not env.destination.exists()


def test_the_command_takes_the_maps_by_name_and_refuses_an_unknown_one(env):
    env.write("shared/malas/Vendors.json", spawner(10, 10, 0, 2, 1, "00:05:00", "00:10:00", "Minter"))
    arguments = ["modernuo-spawns", "--source", str(env.source), "--mobiles", str(env.mobiles), "--destination", str(env.destination)]

    assert main([*arguments, "--maps", "malas, Tokuno"], env.output, env.error) == 0
    assert (env.destination / "malas" / "modernuo_vendors.toml").exists()
    assert main([*arguments, "--maps", "narnia"], env.output, env.error) == 2
    assert "Unknown map: narnia" in env.error.getvalue()
