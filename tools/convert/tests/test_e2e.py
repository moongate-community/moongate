"""End to end: the installed ``moongate-convert`` command, run as a process, rebuilds every file it owns in a copy of ``moongate_root``.

Opt in with ``MOONGATE_MODERNUO_DIR`` (a ModernUO checkout). The copy starts as the shipped ``templates`` and ``data/locations.toml``; all the
converters run on it in the order an operator would, each as its own process, and the copy must come out as the shipped tree: the same
files with the same bytes. The one difference allowed is the thief and the ranger shops, which the vendors converter makes and nobody shipped.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

REPOSITORY = Path(__file__).resolve().parents[3]
ROOT = REPOSITORY / "moongate_root"
SOURCE = os.environ.get("MOONGATE_MODERNUO_DIR")
COMMAND = Path(sys.executable).parent / "moongate-convert"

pytestmark = pytest.mark.skipif(SOURCE is None, reason="MOONGATE_MODERNUO_DIR is not set")

NOT_SHIPPED = {"templates/shops/ranger.toml", "templates/shops/thief.toml"}


def run(*arguments: str | Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run([str(COMMAND), *map(str, arguments)], capture_output=True, text=True, check=False)


def tree(root: Path) -> dict[str, bytes]:
    files = [path for path in (root / "templates").rglob("*") if path.is_file()] + [root / "data" / "locations.toml"]

    return {path.relative_to(root).as_posix(): path.read_bytes() for path in files}


def test_every_converter_rebuilds_the_shipped_tree(tmp_path):
    source = Path(SOURCE or "")
    data = source / "Distribution" / "Data"
    content = source / "Projects" / "UOContent"
    copy = tmp_path / "root"
    shutil.copytree(ROOT / "templates", copy / "templates")
    (copy / "data").mkdir()
    shutil.copy(ROOT / "data" / "locations.toml", copy / "data" / "locations.toml")
    templates = copy / "templates"

    runs = [
        ("modernuo-signs", "--source", data / "signs.cfg", "--destination", templates / "decorations"),
        ("modernuo-teleporters", "--source", data / "teleporters.json", "--destination", templates / "decorations"),
        ("modernuo-locations", "--source", data / "Locations", "--destination", copy / "data" / "locations.toml"),
        ("modernuo-chests", "--source", data / "Spawns", "--destination", templates / "spawns"),
        ("modernuo-books", "--source", source / "Projects", "--destination", templates / "books" / "modernuo"),
        ("modernuo-vendors", "--source", content, "--items", templates / "items", "--mobiles", templates / "mobiles", "--destination", templates / "shops"),
        (
            "modernuo-guildmasters", "--source", content, "--items", templates / "items", "--mobiles", templates / "mobiles",
            "--npc-lists", templates / "npc_lists",
        ),
        ("modernuo-spawns", "--source", data / "Spawns", "--maps", "malas,tokuno,termur", "--mobiles", templates / "mobiles", "--destination", templates / "spawns"),
        (
            "modernuo-spawns", "--source", data / "Spawns", "--maps", "felucca,trammel,ilshenar", "--mobiles", templates / "mobiles",
            "--destination", templates / "spawns", "--only", "Guildmaster",
        ),
    ]

    for arguments in runs:
        result = run(*arguments)

        assert result.returncode == 0, f"{arguments[0]}: {result.stderr}"
        assert result.stdout.strip(), f"{arguments[0]} reported nothing"

        if arguments[0] == "modernuo-books":
            assert "62 books, 738 pages" in result.stdout

    produced, shipped = tree(copy), tree(ROOT)

    assert set(produced) - set(shipped) == NOT_SHIPPED
    assert set(shipped) <= set(produced)

    changed = [name for name in shipped if produced[name] != shipped[name]]

    assert changed == []


def test_a_missing_source_exits_2_and_writes_nothing(tmp_path):
    result = run("modernuo-signs", "--source", tmp_path / "missing.cfg", "--destination", tmp_path / "out")

    assert result.returncode == 2
    assert result.stderr.strip()
    assert not (tmp_path / "out").exists()
