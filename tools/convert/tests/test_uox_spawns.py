"""The spawn pass of ``uox``: UOX3's NPC lists and spawn regions, ported from the C# tests.

The C# tests run the whole command, so the mobile pass writes the mobile templates the lists and the spawns name; these write the mobile
templates themselves (an orc, a troll and a gorilla) and run the pass alone.
"""

from __future__ import annotations

import pytest
from conftest import read_toml

from moongate_convert import uox, uox_spawns

MOBILES = ("orc", "troll", "gorilla")
LISTS = "[NPCLIST jungle]\n{\norc\n}\n[NPCLIST trolls]\n{\ntroll\n}\n"


def region(number: int, world: int) -> str:
    return f"[REGIONSPAWN {number}]\n{{\nNPC=orc\nMAXNPCS=1\nX1=1\nY1=1\nX2=5\nY2=5\nWORLD={world}\n}}\n"


class Env:
    def __init__(self, workspace) -> None:
        self.workspace = workspace
        self.code: int | None = None
        mobile_file = workspace.mobile_destination / "monsters.toml"
        mobile_file.parent.mkdir(parents=True, exist_ok=True)
        mobile_file.write_text("".join(f'[[mobile]]\nid = "{mobile}"\n\n' for mobile in MOBILES), encoding="utf-8")

    def write(self, relative: str, content: str) -> None:
        path = self.workspace.mobile_source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def sources(self, npc_lists: str | None = None, spawns: str | None = None) -> None:
        self.write("npc/npclists/npclists.dfn", LISTS if npc_lists is None else npc_lists)
        self.write("spawn/felucca/spawn_felucca_town_test.dfn", spawns or "")

    def run(self) -> int:
        workspace = self.workspace
        self.code = uox_spawns.run(
            workspace.mobile_source,
            workspace.mobile_destination,
            workspace.npc_lists_destination,
            workspace.spawns_destination,
            workspace.output,
            workspace.error,
        )

        return self.code

    def lists(self, file: str = "npclists.toml") -> dict[str, dict]:
        return {npc_list["id"]: npc_list for npc_list in read_toml(self.workspace.npc_lists_destination / file)["npc_list"]}

    def spawns(self, map_name: str, file: str = "town_test.toml") -> list[dict]:
        return read_toml(self.workspace.spawns_destination / map_name / file)["spawn"]

    @property
    def combined(self) -> str:
        return self.workspace.combined


@pytest.fixture
def env(uox_workspace) -> Env:
    return Env(uox_workspace)


def describe(entry: dict) -> str:
    return f"{entry['mobile_id']}:{entry['weight']}" if "mobile_id" in entry else f"list {entry['npc_list_id']}:{entry['weight']}"


def test_npc_lists_become_weighted_lists_of_mobiles_and_nested_lists_dropping_unknown_ones(env):
    env.sources(npc_lists="[NPCLIST jungle]\n{\n20|Gorilla\norc\n7|NPCLIST=trolls\nunicorn\n}\n[NPCLIST trolls]\n{\ntroll\n}\n")

    assert env.run() == 0, env.combined

    lists = env.lists()
    assert [describe(entry) for entry in lists["jungle"]["entries"]] == ["gorilla:20", "orc:1", "list trolls:7"]
    assert [entry["mobile_id"] for entry in lists["trolls"]["entries"]] == ["troll"]


def test_spawn_regions_become_spawns_on_their_map_with_get_inheritance_and_item_only_regions_skipped(env):
    env.sources(
        spawns="""\
[REGIONSPAWN 0]
{
NAME=The Hammer And Anvil
NPC=orc
MAXNPCS=2
X1=1422
Y1=1547
X2=1426
Y2=1550
WORLD=1
MINTIME=480
MAXTIME=600
CALL=1
ONLYOUTSIDE=1
EXCLUDEAREA=1423,1548,1424,1549
PREFZ=22
}
[REGIONSPAWN 1]
{
GET=0
WORLD=1
NAME=Next Door
NPCLIST=jungle
NPCLIST=trolls
DEFZ=36
}
[REGIONSPAWN 2]
{
NAME=Treasure
ITEMLIST=dungeon_treasure
MAXITEMS=6
X1=1
Y1=1
X2=5
Y2=5
WORLD=1
}
"""
    )

    assert env.run() == 0, env.combined

    shop, next_door = env.spawns("trammel")
    assert [shop["id"], next_door["id"]] == ["trammel_0", "trammel_1"]
    assert (shop["map"], shop["name"], shop["max"], shop["min_minutes"], shop["max_minutes"], shop["call"], shop["only_outside"]) == (
        "trammel", "The Hammer And Anvil", 2, 480, 600, 1, True,
    )  # fmt: skip
    assert (shop["pref_z"], "z" in shop) == (22, False)
    assert shop["mobile_ids"] == ["orc"]
    assert [(shop["areas"][0][key]) for key in ("x1", "y1", "x2", "y2")] == [1422, 1547, 1426, 1550]
    assert [(shop["exclude"][0][key]) for key in ("x1", "y1", "x2", "y2")] == [1423, 1548, 1424, 1549]
    assert (next_door["name"], next_door["max"], next_door["min_minutes"], next_door["z"]) == ("Next Door", 2, 480, 36)
    assert next_door["mobile_ids"] == []
    assert next_door["npc_list_ids"] == ["jungle", "trolls"]
    assert len(next_door["areas"]) == 1
    assert "1 x spawn region(s) without NPCs skipped" in env.combined


def test_two_files_with_regions_of_the_same_map_and_name_are_written_together(env):
    env.sources(spawns=region(0, world=0))
    env.write("spawn/trammel/spawn_trammel_town_test.dfn", region(1, world=0) + region(2, world=1))

    assert env.run() == 0, env.combined

    assert [spawn["id"] for spawn in env.spawns("felucca")] == ["felucca_0", "felucca_1"]
    assert [spawn["id"] for spawn in env.spawns("trammel")] == ["trammel_2"]


def test_a_region_number_defined_twice_keeps_the_last_definition_as_uox3_does(env):
    env.sources(spawns=region(0, world=0) + region(0, world=0).replace("MAXNPCS=1", "MAXNPCS=4"))

    assert env.run() == 0, env.combined

    (spawn,) = env.spawns("felucca")
    assert spawn["max"] == 4
    assert "1 x duplicate spawn region" in env.combined


def test_an_unweighted_nested_list_is_spliced_and_a_weighted_one_stays_a_pick(env):
    env.sources(npc_lists="[NPCLIST covetous]\n{\nNPCLIST=trolls\ngorilla\n2|NPCLIST=trolls\n}\n[NPCLIST trolls]\n{\ntroll\norc\n}\n")

    assert env.run() == 0, env.combined

    assert [describe(entry) for entry in env.lists()["covetous"]["entries"]] == ["troll:1", "orc:1", "gorilla:1", "list trolls:2"]


def test_a_region_getting_one_on_another_map_stays_on_its_folder_map_and_inherits_no_npc(env):
    env.sources(spawns=region(0, world=0))
    env.write(
        "spawn/trammel/spawn_trammel_town_test.dfn",
        "[REGIONSPAWN 1]\n{\nGET=0\nNPC=troll\n}\n[REGIONSPAWN 2]\n{\nGET=0\nNAME=No npc of its own\n}\n",
    )

    assert env.run() == 0, env.combined

    (trammel,) = env.spawns("trammel")
    assert (trammel["id"], trammel["map"]) == ("trammel_1", "trammel")
    assert trammel["mobile_ids"] == ["troll"]
    assert trammel["areas"][0]["x1"] == 1


def test_a_region_of_another_era_is_skipped(env):
    env.sources(
        spawns=region(0, world=0).replace("WORLD=0", "WORLD=0\nERAS=UO,T2A,UOR,TD")
        + region(1, world=0).replace("WORLD=0", "WORLD=0\nERAS=LBR,AOS,TOL")
    )

    assert env.run() == 0, env.combined

    assert [spawn["id"] for spawn in env.spawns("felucca")] == ["felucca_1"]
    assert "1 x spawn region of another era skipped" in env.combined


def test_reversed_exclude_corners_and_times_are_sorted_and_the_output_is_verified(env):
    env.sources(spawns=region(0, world=0).replace("WORLD=0", "WORLD=0\nEXCLUDEAREA=4,4,2,2\nMINTIME=10\nMAXTIME=5"))

    assert env.run() == 0, env.combined

    (spawn,) = env.spawns("felucca")
    assert [spawn["exclude"][0][key] for key in ("x1", "y1", "x2", "y2")] == [2, 2, 4, 4]
    assert (spawn["min_minutes"], spawn["max_minutes"]) == (5, 10)
    assert "read back from disk" in env.combined


def test_the_ilishenar_folder_names_the_files_without_their_prefix(env):
    env.sources()
    env.write("spawn/ilishenar/spawn_ilshenar_world_general.dfn", region(0, world=2))

    assert env.run() == 0, env.combined

    assert (env.workspace.spawns_destination / "ilshenar" / "world_general.toml").is_file(), env.combined


def test_a_spawn_of_an_unknown_mobile_is_dropped(env):
    env.sources(spawns="[REGIONSPAWN 0]\n{\nNAME=Nobody\nNPC=dragon_king\nMAXNPCS=1\nX1=1\nY1=1\nX2=5\nY2=5\nWORLD=0\n}\n")

    assert env.run() == 0, env.combined

    assert not (env.workspace.spawns_destination / "felucca" / "town_test.toml").exists()
    assert "unresolved spawn" in env.combined


def test_the_spawn_options_need_the_mobile_source(uox_workspace):
    uox_workspace.write_source("items.dfn", "[coin]\n{\nid=0x0eed\n}\n")

    code = uox.run(
        uox_workspace.source,
        uox_workspace.destination,
        None,
        uox_workspace.output,
        uox_workspace.error,
        npc_lists_destination=uox_workspace.npc_lists_destination,
        spawns_destination=uox_workspace.spawns_destination,
    )

    assert code == 2
    assert "--npc-lists-destination and --spawns-destination need --mobile-source" in uox_workspace.error.getvalue()


def test_the_spawn_options_go_together(uox_workspace):
    uox_workspace.write_source("items.dfn", "[coin]\n{\nid=0x0eed\n}\n")

    code = uox.run(
        uox_workspace.source,
        uox_workspace.destination,
        None,
        uox_workspace.output,
        uox_workspace.error,
        npc_lists_destination=uox_workspace.npc_lists_destination,
    )

    assert code == 2
    assert "--npc-lists-destination and --spawns-destination go together" in uox_workspace.error.getvalue()


def test_an_npc_list_cycle_ends_and_a_list_left_empty_takes_the_entries_naming_it_with_it(env):
    env.sources(npc_lists="[NPCLIST a]\n{\nNPCLIST=b\norc\n}\n[NPCLIST b]\n{\nNPCLIST=a\ntroll\n}\n[NPCLIST ghosts]\n{\nunicorn\n}\n[NPCLIST haunted]\n{\n3|NPCLIST=ghosts\norc\n}\n")

    assert env.run() == 0, env.combined

    lists = env.lists()
    assert [describe(entry) for entry in lists["a"]["entries"]] == ["troll:1", "orc:1"]
    assert [describe(entry) for entry in lists["haunted"]["entries"]] == ["orc:1"]
    assert "ghosts" not in lists
    assert "1 x empty npc list" in env.combined


def test_a_duplicate_npc_list_is_counted_and_the_first_is_kept(env):
    env.sources(npc_lists="[NPCLIST jungle]\n{\norc\n}\n[NPCLIST Jungle]\n{\ntroll\n}\n")

    assert env.run() == 0, env.combined

    assert [describe(entry) for entry in env.lists()["jungle"]["entries"]] == ["orc:1"]
    assert "1 x duplicate npc list" in env.combined


def test_a_region_with_several_areas_is_written_with_a_blank_line_between_them(env):
    env.sources(spawns=region(0, world=0).replace("WORLD=0", "WORLD=0\nEXCLUDEAREA=1,1,2,2\nEXCLUDEAREA=3,3,4,4"))

    assert env.run() == 0, env.combined

    text = (env.workspace.spawns_destination / "felucca" / "town_test.toml").read_text(encoding="utf-8")
    assert "[[spawn.exclude]]\nx1 = 1\ny1 = 1\nx2 = 2\ny2 = 2\n\n[[spawn.exclude]]\nx1 = 3\n" in text


def test_a_spawn_that_fails_the_loader_checks_is_reported_when_read_back(env):
    env.sources(spawns=region(0, world=0))
    stale = env.workspace.spawns_destination / "felucca" / "stale.toml"
    stale.parent.mkdir(parents=True)
    stale.write_text(
        '[[spawn]]\nid = "felucca_9"\nmap = "felucca"\nmobile_ids = ["dragon_king"]\nnpc_list_ids = []\nmax = 1\nmin_minutes = 9\nmax_minutes = 2\ncall = 1\n',
        encoding="utf-8",
    )

    assert env.run() == 1

    error = env.workspace.error.getvalue()
    assert "Verification failed: spawn 'felucca_9' names a mobile or a list that does not resolve." in error
    assert "Verification failed: spawn 'felucca_9' has a max, call, times or area the loader refuses." in error
    assert "2 verification error(s) found reading the converted spawns back." in error
