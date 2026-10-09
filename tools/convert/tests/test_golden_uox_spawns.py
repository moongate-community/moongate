"""The starting items, NPC lists and spawn regions of ``uox`` against the real data of UOX3.

Opt in with ``MOONGATE_UOX3_DIR`` set to a checkout of UOX3 (the folder that holds ``data``). The mobile templates the lists and the spawns
name are the shipped ones, since the mobile pass is not what is tested here. The shipped files are older than the converter in a few ways,
each of which the tests below name and check:

* the spawn files were written before a spawn had ``item_ids``, so the converter's output has one ``item_ids = []`` line more per spawn;
* ``npclists_towns.toml`` was written before the thief guildmaster had a mobile, so it lacks that list;
* ``starting_items.toml`` has the empty ``book_values`` table Tomlyn writes for each entry only in the converter's output, and its common set
  was edited by hand afterwards (the blank book and the welcome letter).

"""

from __future__ import annotations

import io
import os
import re
import tomllib
from pathlib import Path

import pytest
from uox_support import item_index

from moongate_convert import uox_spawns, uox_starting_items
from moongate_convert.cli import main

REPOSITORY = Path(__file__).resolve().parents[3]
ROOT = REPOSITORY / "moongate_root"
SOURCE = os.environ.get("MOONGATE_UOX3_DIR")

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
