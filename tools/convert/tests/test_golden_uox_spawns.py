"""The starting items, NPC lists and spawn regions of ``uox`` against the real data of UOX3.

Opt in with ``MOONGATE_UOX3_DIR`` set to a checkout of UOX3 (the folder that holds ``data``). The mobile templates the lists and the spawns
name are the shipped ones, since the mobile pass is not what is tested here. The shipped files are older than the converter in a few ways,
each of which the tests below name and check:

* the spawn files were written before a spawn had ``item_ids``, so the converter's output has one ``item_ids = []`` line more per spawn;
* ``npclists_towns.toml`` was written before the thief guildmaster had a mobile, so it lacks that list;
* ``starting_items.toml`` has the empty ``book_values`` table Tomlyn writes for each entry only in the converter's output, and its common set
  was edited by hand afterwards (the blank book and the welcome letter).

A second test, with ``MOONGATE_MGCTL`` set to the path of a built ``mgctl``, runs the C# converter on the same source and on some small
sources of edge cases, and requires the same files and the same report.
"""

from __future__ import annotations

import io
import os
import re
import subprocess
import tomllib
from pathlib import Path

import pytest
from uox_support import item_index

from moongate_convert import uox_spawns, uox_starting_items
from moongate_convert.cli import main

REPOSITORY = Path(__file__).resolve().parents[3]
ROOT = REPOSITORY / "moongate_root"
SOURCE = os.environ.get("MOONGATE_UOX3_DIR")
MGCTL = os.environ.get("MOONGATE_MGCTL")

pytestmark = pytest.mark.skipif(SOURCE is None, reason="MOONGATE_UOX3_DIR is not set")

# The entries of the common set that were edited by hand: the plain book of the converter became the blank book of the server, and the
# welcome letter was added.
HAND_EDITED_ITEMS = {"0x0fef_book", "readable_book", "readable_scroll"}


def data() -> Path:
    return Path(SOURCE or "") / "data"


def tree(root: Path) -> dict[str, bytes]:
    return {path.relative_to(root).as_posix(): path.read_bytes() for path in sorted(root.rglob("*.toml"))}


def run_passes(mobile_source: Path, items_source: Path, mobiles: Path, destination: Path):
    """The three passes, as ``uox`` runs them after the mobile pass, against the mobile templates of a folder."""
    output, error = io.StringIO(), io.StringIO()
    codes = [
        uox_starting_items.run(mobile_source, destination / "starting_items.toml", item_index(items_source), output, error),
        uox_spawns.run(mobile_source, mobiles, destination / "npc_lists", destination / "spawns", output, error),
    ]

    return codes, output.getvalue(), error.getvalue()


def test_the_spawns_and_lists_are_the_shipped_ones_apart_from_what_is_named(tmp_path):
    codes, output, error = run_passes(data() / "dfndata", data() / "dfndata" / "items", ROOT / "templates" / "mobiles", tmp_path)

    assert codes == [0, 0], error

    # Every spawn file that is shipped is made, byte for byte, apart from the item_ids line a spawn did not have then.
    shipped = ROOT / "templates" / "spawns"
    made = tree(tmp_path / "spawns")
    common = sorted(name for name in made if (shipped / name).is_file())

    assert len(made) == len(common) == 96
    assert [name for name in common if re.sub(rb"item_ids = \[\]\n", b"", made[name]) != (shipped / name).read_bytes()] == []

    # Every list file that is shipped is made, byte for byte, but the one that lacks the thief guildmaster.
    lists_shipped = ROOT / "templates" / "npc_lists"
    lists = tree(tmp_path / "npc_lists")
    stale = [name for name, text in lists.items() if (lists_shipped / name).is_file() and text != (lists_shipped / name).read_bytes()]

    assert stale == ["npclists_towns.toml"]
    produced = tomllib.loads(lists["npclists_towns.toml"].decode())["npc_list"]
    kept = tomllib.loads((lists_shipped / "npclists_towns.toml").read_text(encoding="utf-8"))["npc_list"]
    assert [npc_list["id"] for npc_list in produced if npc_list not in kept] == ["thiefguildmaster"]
    assert [npc_list for npc_list in produced if npc_list in kept] == kept

    assert "Verified 446 npc list(s) and 2778 spawn region(s) read back from disk" in output


def sets_without_hand_edits(text: str) -> list[dict]:
    """The sets of a starting items file, with no empty ``book_values`` table and none of the entries edited by hand."""
    sets = tomllib.loads(text)["set"]

    for entry_set in sets:
        entry_set["items"] = [
            {key: value for key, value in entry.items() if key != "book_values"}
            for entry in entry_set["items"]
            if not (set(entry["items"]) & HAND_EDITED_ITEMS)
        ]

    return sets


def test_the_starting_items_are_the_shipped_ones_apart_from_the_hand_edits(tmp_path):
    codes, _, error = run_passes(data() / "dfndata", data() / "dfndata" / "items", ROOT / "templates" / "mobiles", tmp_path)

    assert codes == [0, 0], error

    made = (tmp_path / "starting_items.toml").read_text(encoding="utf-8")
    shipped = (ROOT / "data" / "starting_items.toml").read_text(encoding="utf-8")

    assert sets_without_hand_edits(made) == sets_without_hand_edits(shipped)
    # What the converter writes is what Tomlyn writes: an empty book_values table after every entry.
    # The header names the table once.
    assert made.count("[set.items.book_values]") == made.count("[[set.items]]") - 1 == 145


def convert_with_csharp(workspace: Path, mobile_source: Path, items_source: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            MGCTL or "", "convert", "uox",
            "--source", str(items_source),
            "--destination", str(workspace / "items"),
            "--mobile-source", str(mobile_source),
            "--mobile-destination", str(workspace / "mobiles"),
            "--names-destination", str(workspace / "names.toml"),
            "--starting-items-destination", str(workspace / "starting_items.toml"),
            "--npc-lists-destination", str(workspace / "npc_lists"),
            "--spawns-destination", str(workspace / "spawns"),
        ],
        capture_output=True,
        text=True,
        check=False,
    )  # fmt: skip


def the_passes_report(text: str, workspace: Path) -> list[str]:
    """The lines the starting items and the spawn passes print, with the workspace taken out of the paths."""
    lines = text.replace(str(workspace), "<workspace>").splitlines()
    start = next(index for index, line in enumerate(lines) if line.startswith("Converted ") and "starting item set(s)" in line)

    return lines[start:]


def assert_same_as_csharp(tmp_path: Path, mobile_source: Path, items_source: Path) -> None:
    csharp, python = tmp_path / "csharp", tmp_path / "python"
    csharp.mkdir(parents=True)
    python.mkdir(parents=True)
    reference = convert_with_csharp(csharp, mobile_source, items_source)

    assert reference.returncode == 0, reference.stderr

    codes, output, error = run_passes(mobile_source, items_source, csharp / "mobiles", python)

    assert codes == [0, 0], error
    # Something was made, so equal empty folders cannot pass.
    assert tree(python / "npc_lists") and tree(python / "spawns")
    assert tree(python / "npc_lists") == tree(csharp / "npc_lists")
    assert tree(python / "spawns") == tree(csharp / "spawns")
    assert (python / "starting_items.toml").read_bytes() == (csharp / "starting_items.toml").read_bytes()
    assert the_passes_report(output, python) == the_passes_report(reference.stdout, csharp)


@pytest.mark.skipif(MGCTL is None, reason="MOONGATE_MGCTL is not set")
def test_the_csharp_converter_gives_the_same_files_and_report_on_the_real_data(tmp_path):
    assert_same_as_csharp(tmp_path, data() / "dfndata", data() / "dfndata" / "items")


EDGE_FILES = {
    "items/items.dfn": "[0x0eed]\n{\nid=0x0eed\nname=gold coin\n}\n[0x103b]\n{\nid=0x103b\nname=bread loaf\n}\n"
    "[0x0f7a]\n{\nid=0x0f7a\nname=black pearl\n}\n[alias]\n{\nget=0x0f7a\n}\n[0x1f03]\n{\nid=0x1f03\nname=robe\n}\n"
    "[ITEMLIST 6]\n{\nalias\n2|0x1f03\nblank\n}\n",
    "newbie/newbie.dfn": "[BESTSKILL 0]\n{\nPACKITEM=0x0f7a,3,1\nEQUIPITEM=0x1f03,0x4ca,0\nPACKITEM=listobject6,2\nBAD=1\n}\n"
    "[BESTSKILL 56]\n{\nPACKITEM=0x0f7a\n}\n[BESTSKILL 256]\n{\nPACKITEM=0x0f7a,-4\n}\n[DEFAULT ELF FEMALE]\n{\nEQUIPITEM=0x1f03\n}\n",
    "npc/namelists.dfn": "",
    "npc/monsters.dfn": "[orc]\n{\nNAME=an orc\nID=0x0011\n}\n[troll]\n{\nNAME=a troll\nID=0x0036\n}\n[gorilla]\n{\nNAME=a gorilla\nID=0x001D\n}\n",
    "npc/npclists/npclists.dfn": "[NPCLIST a]\n{\nNPCLIST=b\norc\n}\n[NPCLIST b]\n{\nNPCLIST=a\ntroll\n}\n[NPCLIST ghosts]\n{\nunicorn\n}\n"
    "[NPCLIST haunted]\n{\n3|NPCLIST=ghosts\norc\n}\n[NPCLIST Jungle]\n{\n20|Gorilla\n0|orc\n x|troll\n}\n[NPCLIST jungle]\n{\ntroll\n}\n"
    "[NPCLIST covetous]\n{\nNPCLIST=trolls\ngorilla\n2|NPCLIST=trolls\n}\n[NPCLIST trolls]\n{\ntroll\norc\n}\n",
    "spawn/felucca/spawn_felucca_town_test.dfn": "[REGIONSPAWN 0]\n{\nNAME=Shop\nNPC=orc\nNPCLIST=jungle\nMAXNPC=3\nX1=9\nY1=8\nX2=1\nY2=2\nMINTIME=10\nMAXTIME=5\n"
    "ONLYOUTSIDE=1\nEXCLUDEAREA=4,4,2,2\nEXCLUDEAREA=1,1,2\nEXCLUDEAREA=6,6,7,7\nPREFZ=0x10\nDEFZ=-3\nCALL=0\n}\n"
    "[REGIONSPAWN 1]\n{\nGET=0\nGET=1\nNAME=Child\nWORLD=0x1\nERAS=LBR,TOL\n}\n[REGIONSPAWN 2]\n{\nGET=1\nNPC=troll\nX1=1\nY1=1\nX2=2\nY2=2\nMAXNPCS=1\n}\n"
    "[REGIONSPAWN 3]\n{\nNPC=orc\nMAXNPCS=1\nX1=1\nY1=1\nX2=2\nY2=2\nERAS=UO\n}\n[REGIONSPAWN 4]\n{\nNPC=orc\nMAXNPCS=0\nX1=1\nY1=1\nX2=2\nY2=2\n}\n"
    "[REGIONSPAWN 5]\n{\nNPC=orc\nMAXNPCS=1\n}\n[REGIONSPAWN 5]\n{\nNPC=troll\nMAXNPCS=1\nX1=1\nY1=1\nX2=2\nY2=2\nWORLD=9\n}\n",
    "spawn/tokuno/spawn_tokuno_Tokuno Lands.dfn": "[REGIONSPAWN 7]\n{\nNPC=gorilla\nMAXNPCS=2\nX1=1\nY1=1\nX2=2\nY2=2\n}\n",
    "spawn/ilishenar/spawn_ilshenar_world_general.dfn": "[REGIONSPAWN 0]\n{\nNPC=orc\nMAXNPCS=1\nX1=1\nY1=1\nX2=5\nY2=5\n}\n",
}


@pytest.mark.skipif(MGCTL is None, reason="MOONGATE_MGCTL is not set")
def test_the_csharp_converter_gives_the_same_files_and_report_on_the_edge_cases(tmp_path):
    mobile_source = tmp_path / "dfndata"

    for relative, content in EDGE_FILES.items():
        path = mobile_source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    (tmp_path / "dictionaries").mkdir()
    (tmp_path / "dictionaries" / "dictionary.ENG").write_text("", encoding="utf-8")

    assert_same_as_csharp(tmp_path / "run", mobile_source, mobile_source / "items")


def test_the_command_runs_the_passes_it_is_given(tmp_path):
    """``main`` takes the spawn options and rejects them without a mobile source, as ``mgctl convert uox`` does."""
    items = tmp_path / "items.dfn"
    items.write_text("[coin]\n{\nid=0x0eed\n}\n", encoding="utf-8")
    output, error = io.StringIO(), io.StringIO()

    code = main(
        ["uox", "--source", str(items), "--destination", str(tmp_path / "out"), "--npc-lists-destination", str(tmp_path / "l"), "--spawns-destination", str(tmp_path / "s")],
        output,
        error,
    )

    assert code == 2
    assert "need --mobile-source" in error.getvalue()
