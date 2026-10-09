"""The converters against the real sources of ModernUO: what they write must be what is shipped in ``moongate_root``.

Opt in with ``MOONGATE_MODERNUO_DIR`` set to a checkout of ModernUO (the folder that holds ``Distribution`` and ``Projects``). A difference
is either a bug of the port or a change of the source since the shipped files were made: the second is settled by running the converter
again and shipping its output.
"""

from __future__ import annotations

import io
import os
import shutil
from pathlib import Path

import pytest

from moongate_convert.cli import main

REPOSITORY = Path(__file__).resolve().parents[3]
ROOT = REPOSITORY / "moongate_root"
SOURCE = os.environ.get("MOONGATE_MODERNUO_DIR")

pytestmark = pytest.mark.skipif(SOURCE is None, reason="MOONGATE_MODERNUO_DIR is not set")


def compare(produced: Path, shipped: Path, names: list[str]) -> None:
    """Every produced file is the shipped one, byte for byte; every shipped file of those names was produced."""
    made = sorted(path.relative_to(produced).as_posix() for path in produced.rglob("*.toml") if path.name in names)
    kept = sorted(path.relative_to(shipped).as_posix() for path in shipped.rglob("*.toml") if path.name in names)

    assert made == kept

    for name in made:
        assert (produced / name).read_bytes() == (shipped / name).read_bytes(), name


def data(*parts: str) -> Path:
    return Path(SOURCE or "") / "Distribution" / "Data" / Path(*parts)


def test_signs(tmp_path, convert):
    assert convert("modernuo-signs", data("signs.cfg"), tmp_path).code == 0

    compare(tmp_path, ROOT / "templates" / "decorations", ["signs.toml", "_signs.toml"])


def test_teleporters(tmp_path, convert):
    assert convert("modernuo-teleporters", data("teleporters.json"), tmp_path).code == 0

    compare(tmp_path, ROOT / "templates" / "decorations", ["teleporters.toml"])


def test_locations(tmp_path, convert):
    assert convert("modernuo-locations", data("Locations"), tmp_path / "locations.toml").code == 0

    assert (tmp_path / "locations.toml").read_bytes() == (ROOT / "data" / "locations.toml").read_bytes()


def test_chests(tmp_path, convert):
    assert convert("modernuo-chests", data("Spawns"), tmp_path).code == 0

    compare(tmp_path, ROOT / "templates" / "spawns", ["treasure_chests.toml"])


def spawns_command(destination: Path, maps: str, *extra: str):
    output, error = io.StringIO(), io.StringIO()
    code = main(
        [
            "modernuo-spawns",
            "--source", str(data("Spawns")),
            "--maps", maps,
            "--mobiles", str(ROOT / "templates" / "mobiles"),
            "--destination", str(destination),
            *extra,
        ],
        output,
        error,
    )

    assert code == 0, error.getvalue()


def test_spawns(tmp_path):
    shipped = ROOT / "templates" / "spawns"
    spawns_command(tmp_path, "malas,tokuno,termur")

    # The shipped modernuo_ files of the maps UOX3 has no spawns for: all but the guildmasters, which another run writes.
    made = sorted(path.relative_to(tmp_path).as_posix() for path in tmp_path.rglob("modernuo_*.toml"))
    kept = sorted(
        path.relative_to(shipped).as_posix()
        for path in shipped.rglob("modernuo_*.toml")
        if path.name != "modernuo_guildmasters.toml"
    )

    assert made == kept

    for name in made:
        assert (tmp_path / name).read_bytes() == (shipped / name).read_bytes(), name


def test_spawns_of_the_guildmasters(tmp_path):
    shipped = ROOT / "templates" / "spawns"
    spawns_command(tmp_path, "felucca,trammel,ilshenar", "--only", "Guildmaster")

    made = sorted(path.relative_to(tmp_path).as_posix() for path in tmp_path.rglob("*.toml"))
    kept = sorted(path.relative_to(shipped).as_posix() for path in shipped.rglob("modernuo_guildmasters.toml"))

    assert made == kept

    for name in made:
        assert (tmp_path / name).read_bytes() == (shipped / name).read_bytes(), name


def test_books(tmp_path, convert):
    # A refresh keeps the translations of the files it finds, so it starts from the shipped ones.
    shipped = ROOT / "templates" / "books" / "modernuo"
    shutil.copytree(shipped, tmp_path / "books")

    result = convert("modernuo-books", Path(SOURCE or "") / "Projects", tmp_path / "books")

    # The files were copied first: the report proves the converter read the sources and wrote the books.
    assert result.code == 0
    assert "62 books, 738 pages" in result.output

    compare(tmp_path / "books", shipped, [path.name for path in shipped.glob("*.toml")])


def test_vendors(tmp_path):
    shipped = ROOT / "templates" / "shops"
    code = main(
        [
            "modernuo-vendors",
            "--source", str(Path(SOURCE or "") / "Projects" / "UOContent"),
            "--items", str(ROOT / "templates" / "items"),
            "--mobiles", str(ROOT / "templates" / "mobiles"),
            "--destination", str(tmp_path),
        ],
        io.StringIO(),
        io.StringIO(),
    )

    assert code == 0

    made = {path.name for path in tmp_path.glob("*.toml")}
    kept = {path.name for path in shipped.glob("*.toml")}

    # The thief and the ranger have mobile templates, so the converter makes a shop for each; they were never shipped.
    assert kept <= made
    assert made - kept == {"ranger.toml", "thief.toml"}

    for name in kept:
        assert (tmp_path / name).read_bytes() == (shipped / name).read_bytes(), name


def test_guildmasters(tmp_path):
    code = main(
        [
            "modernuo-guildmasters",
            "--source", str(Path(SOURCE or "") / "Projects" / "UOContent"),
            "--items", str(ROOT / "templates" / "items"),
            "--mobiles", str(tmp_path / "mobiles"),
            "--npc-lists", str(tmp_path / "npc_lists"),
        ],
        io.StringIO(),
        io.StringIO(),
    )

    assert code == 0
    assert (tmp_path / "mobiles" / "guildmasters.toml").read_bytes() == (ROOT / "templates" / "mobiles" / "guildmasters.toml").read_bytes()
    assert (tmp_path / "npc_lists" / "npclists_guildmasters.toml").read_bytes() == (ROOT / "templates" / "npc_lists" / "npclists_guildmasters.toml").read_bytes()


def test_taming(tmp_path):
    code = main(
        [
            "modernuo-taming",
            "--source", str(Path(SOURCE or "") / "Projects" / "UOContent"),
            "--templates", str(ROOT / "templates" / "mobiles"),
            "--destination", str(tmp_path),
        ],
        io.StringIO(),
        io.StringIO(),
    )

    assert code == 0
    assert (tmp_path / "taming.toml").read_bytes() == (ROOT / "data" / "taming.toml").read_bytes()
