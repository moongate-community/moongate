"""The ``uox`` command against the real data of UOX3.

Opt in with ``MOONGATE_UOX3_DIR`` set to a checkout of UOX3 (the folder that holds ``data``). The loot tables are those shipped in
``moongate_root/templates/loots``, byte for byte (the folder holds hand-written tables too). The items are too, apart from the files that were shipped before the converter learned the
combat fields and the kind of weapon (and the ones a script id was added to by hand): ``STALE`` names them, and the test fails when that list
is not what differs, so a refreshed file must leave it.

"""

from __future__ import annotations

import io
import os
from pathlib import Path

import pytest

from moongate_convert.cli import main

REPOSITORY = Path(__file__).resolve().parents[3]
ROOT = REPOSITORY / "moongate_root"
SOURCE = os.environ.get("MOONGATE_UOX3_DIR")

pytestmark = pytest.mark.skipif(SOURCE is None, reason="MOONGATE_UOX3_DIR is not set")

# Shipped before the combat fields (damage, speed, armor, hits, kind of weapon) were converted, or with a script id added by hand.
STALE = {
    "building/decs/dungeon_traps.toml",
    "building/decs/misc.toml",
    "building/lighting.toml",
    "gear/clothing/aos_clothing.toml",
    "gear/clothing/clothing.toml",
    "gear/clothing/footwear.toml",
    "gear/clothing/headwear.toml",
    "gear/clothing/se_headwear.toml",
    "gear/clothing/td_clothing.toml",
    "gear/clothing/td_footwear.toml",
    "gear/clothing/td_headwear.toml",
    "gear/magic_items.toml",
    "gmmenu/gm_skins.toml",
    "houseaddons/house_addons.toml",
    "magic/potions.toml",
    "misc/bod_rewards_blacksmith.toml",
    "misc/christmas.toml",
    "misc/halloween.toml",
    "misc/plantgrowing.toml",
    "skills/resources/tinkering.toml",
    "skills/tools/alchemy.toml",
    "skills/tools/healing.toml",
    "skills/tools/mining.toml",
    "skills/tools/thieving.toml",
}


def options(destination: Path) -> list[str]:
    data = Path(SOURCE or "") / "data"

    return [
        "--source", str(data / "dfndata" / "items"),
        "--destination", str(destination / "items"),
        "--loot-destination", str(destination / "loots"),
        "--scripts-source", str(data / "js"),
    ]  # fmt: skip


def tree(root: Path) -> dict[str, bytes]:
    return {path.relative_to(root).as_posix(): path.read_bytes() for path in sorted(root.rglob("*.toml"))}


def test_the_items_and_loots_are_the_shipped_ones(tmp_path):
    output, error = io.StringIO(), io.StringIO()

    assert main(["uox", *options(tmp_path)], output, error) == 0, error.getvalue()

    shipped_items, shipped_loots = ROOT / "templates" / "items", ROOT / "templates" / "loots"
    items, loots = tree(tmp_path / "items"), tree(tmp_path / "loots")

    assert len(items) == 234 and len(loots) == 71
    # The shipped folders hold hand-written files too (the fillable containers, the treasure maps): only what is converted is compared.
    assert set(loots) <= {path.relative_to(shipped_loots).as_posix() for path in shipped_loots.rglob("*.toml")}
    assert [name for name, text in loots.items() if text != (shipped_loots / name).read_bytes()] == []
    assert {name for name, text in items.items() if text != (shipped_items / name).read_bytes()} == STALE

