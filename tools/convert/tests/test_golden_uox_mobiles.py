"""The mobile pass of the ``uox`` command against the real data of UOX3.

Opt in with ``MOONGATE_UOX3_DIR`` set to a checkout of UOX3 (the folder that holds ``data``). The name lists are the shipped ``names.toml``,
byte for byte. The mobile templates are the shipped ones apart from the files that were edited by hand after they were converted (the script
ids moved by the keys of the converter, the shopkeepers' notoriety, the tuning of the humans, the elementals and the undead): ``STALE`` names
them, and the test fails when that list is not what differs, so a refreshed file must leave it.

The shipped folder holds hand-written mobiles too (``guildmasters.toml``, the cats and the lilly), and no converted file is missing from it.
"""

from __future__ import annotations

import io
import os
import subprocess
from pathlib import Path

import pytest

from moongate_convert.cli import main

REPOSITORY = Path(__file__).resolve().parents[3]
ROOT = REPOSITORY / "moongate_root"
SOURCE = os.environ.get("MOONGATE_UOX3_DIR")
MGCTL = os.environ.get("MOONGATE_MGCTL")

pytestmark = pytest.mark.skipif(SOURCE is None, reason="MOONGATE_UOX3_DIR is not set")

# Converted before the converter wrote the script id where it writes it now, or edited by hand since.
STALE = {
    "agressiveanimals.toml",
    "agressiveplants.toml",
    "animals.toml",
    "arachnids.toml",
    "birds.toml",
    "champions.toml",
    "christmascreatures.toml",
    "clockwork.toml",
    "cursed_base.toml",
    "daemons.toml",
    "dragons.toml",
    "elementals.toml",
    "femalehuman.toml",
    "femalevendors.toml",
    "gargoyles.toml",
    "giants.toml",
    "halloweencreatures.toml",
    "humanoids_base.toml",
    "lbrraces.toml",
    "magicsummon.toml",
    "malehuman.toml",
    "malevendors.toml",
    "miscmonsters.toml",
    "mounts.toml",
    "npc_se/mounts_se.toml",
    "npc_se/undead_se.toml",
    "reptiles.toml",
    "savages.toml",
    "seacreatures.toml",
    "townfolk.toml",
    "undead.toml",
    "vendors.toml",
    "zealots_base.toml",
}

HAND_WRITTEN = {"guildmasters.toml", "moongate_cats.toml", "moongate_lilly.toml"}


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
    ]  # fmt: skip


def tree(root: Path) -> dict[str, bytes]:
    return {path.relative_to(root).as_posix(): path.read_bytes() for path in sorted(root.rglob("*.toml"))}


def test_the_names_and_the_mobiles_are_the_shipped_ones(tmp_path):
    output, error = io.StringIO(), io.StringIO()
    code = main(["uox", *options(tmp_path)], output, error)

    assert code == 0, error.getvalue()
    assert "Converted 671 mobile(s)." in output.getvalue()
    assert (tmp_path / "names.toml").read_bytes() == (ROOT / "data" / "names.toml").read_bytes()

    shipped = ROOT / "templates" / "mobiles"
    made = {path.relative_to(tmp_path / "mobiles").as_posix(): path.read_bytes() for path in sorted((tmp_path / "mobiles").rglob("*.toml"))}
    kept = {path.relative_to(shipped).as_posix() for path in shipped.rglob("*.toml")}

    assert set(made) <= kept
    assert kept - set(made) == HAND_WRITTEN
    assert {name for name, text in made.items() if text != (shipped / name).read_bytes()} == STALE


@pytest.mark.skipif(MGCTL is None, reason="MOONGATE_MGCTL is not set")
def test_the_csharp_converter_gives_the_same_mobiles_names_report_and_warnings(tmp_path):
    csharp, python = tmp_path / "csharp", tmp_path / "python"
    csharp.mkdir()
    reference = subprocess.run([MGCTL or "", "convert", "uox", *options(csharp)], capture_output=True, text=True, check=False)
    output, error = io.StringIO(), io.StringIO()

    code = main(["uox", *options(python)], output, error)

    assert reference.returncode == code == 0
    assert output.getvalue().replace(str(python), "<root>") == reference.stdout.replace(str(csharp), "<root>")
    assert error.getvalue().replace(str(python), "<root>") == reference.stderr.replace(str(csharp), "<root>")
    assert tree(python / "mobiles") == tree(csharp / "mobiles")
    assert (python / "names.toml").read_bytes() == (csharp / "names.toml").read_bytes()
