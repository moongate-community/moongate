"""End to end: the installed ``moongate-convert uox`` command, as a process, with every pass on the real data of UOX3.

Opt in with ``MOONGATE_UOX3_DIR`` (a UOX3 checkout). One run does the items, the loot, the mobiles, the names, the starting items, the npc
lists and the spawns. Every file it writes must be valid TOML. With ``MOONGATE_MGCTL`` (a built ``mgctl``) the C# converter runs the same
command and the two trees, the reports and the exit codes must be identical.
"""

from __future__ import annotations

import os
import subprocess
import sys
import tomllib
from pathlib import Path

import pytest

SOURCE = os.environ.get("MOONGATE_UOX3_DIR")
MGCTL = os.environ.get("MOONGATE_MGCTL")
COMMAND = Path(sys.executable).parent / "moongate-convert"

pytestmark = pytest.mark.skipif(SOURCE is None, reason="MOONGATE_UOX3_DIR is not set")


def options(root: Path) -> list[str]:
    data = Path(SOURCE or "") / "data"

    return [
        "--source", str(data / "dfndata" / "items"),
        "--destination", str(root / "items"),
        "--loot-destination", str(root / "loots"),
        "--scripts-source", str(data / "js"),
        "--mobile-source", str(data / "dfndata"),
        "--mobile-destination", str(root / "mobiles"),
        "--names-destination", str(root / "names.toml"),
        "--starting-items-destination", str(root / "starting_items.toml"),
        "--npc-lists-destination", str(root / "npc_lists"),
        "--spawns-destination", str(root / "spawns"),
    ]  # fmt: skip


def tree(root: Path) -> dict[str, bytes]:
    return {path.relative_to(root).as_posix(): path.read_bytes() for path in sorted(root.rglob("*.toml"))}


def test_every_pass_writes_valid_toml_and_the_report_counts_them(tmp_path):
    result = subprocess.run([str(COMMAND), "uox", *options(tmp_path)], capture_output=True, text=True, check=False)

    assert result.returncode == 0, result.stderr

    files = tree(tmp_path)
    folders = {name.split("/")[0] for name in files}

    assert {"items", "loots", "mobiles", "npc_lists", "spawns", "names.toml", "starting_items.toml"} <= folders
    assert len([name for name in files if name.startswith("items/")]) > 200
    assert len([name for name in files if name.startswith("spawns/")]) > 90

    for name, content in files.items():
        assert tomllib.loads(content.decode("utf-8")) is not None, name

    assert "mobiles" in result.stdout.lower() or "mobile" in result.stdout.lower()


@pytest.mark.skipif(MGCTL is None, reason="MOONGATE_MGCTL is not set")
def test_the_csharp_converter_gives_the_same_tree_report_and_exit_code(tmp_path):
    python, csharp = tmp_path / "python", tmp_path / "csharp"
    csharp.mkdir()
    made = subprocess.run([str(COMMAND), "uox", *options(python)], capture_output=True, text=True, check=False)
    reference = subprocess.run([MGCTL or "", "convert", "uox", *options(csharp)], capture_output=True, text=True, check=False)

    assert made.returncode == reference.returncode == 0
    assert made.stdout.replace(str(python), "<root>") == reference.stdout.replace(str(csharp), "<root>")
    assert made.stderr.replace(str(python), "<root>") == reference.stderr.replace(str(csharp), "<root>")
    assert tree(python) == tree(csharp)


def test_a_missing_source_exits_2_and_writes_nothing(tmp_path):
    result = subprocess.run(
        [str(COMMAND), "uox", "--source", str(tmp_path / "missing"), "--destination", str(tmp_path / "out")],
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 2
    assert result.stderr.strip()
    assert not (tmp_path / "out").exists()
