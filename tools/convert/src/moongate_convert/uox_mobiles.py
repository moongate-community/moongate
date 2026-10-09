"""UOX3's NPCs, creatures and name lists as mobile templates and ``names.toml`` (the mobile pass of ``uox``). Not ported yet."""

from __future__ import annotations

from pathlib import Path
from typing import TextIO

from .item_index import ItemIndex


def run(
    mobile_source: Path,
    mobile_destination: Path,
    names_destination: Path,
    items: ItemIndex,
    output: TextIO,
    error: TextIO,
) -> int:
    """Converts the NPCs of UOX3's ``dfndata`` folder (``mobile_source``: npc/, colors/, creatures/ and newbie/) into mobile templates, one file
    per source file under ``mobile_destination``, and the name lists of ``npc/namelists.dfn`` into the file ``names_destination``
    (``names.toml``). ``items`` is what the item pass computed: the ids the NPCs' equipment and loot resolve against. Writes its report to
    ``output``; returns 0 when done, 1 when reading the output back finds an error, 2 for a bad source (nothing written)."""
    raise NotImplementedError("the UOX3 mobile pass is not ported yet")
