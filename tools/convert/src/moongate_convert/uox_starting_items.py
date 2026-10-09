"""UOX3's starting items (``newbie/newbie.dfn``) as ``starting_items.toml`` (the starting items pass of ``uox``). Not ported yet."""

from __future__ import annotations

from pathlib import Path
from typing import TextIO

from .item_index import ItemIndex


def run(mobile_source: Path, starting_items_destination: Path, items: ItemIndex, output: TextIO, error: TextIO) -> int:
    """Converts ``newbie/newbie.dfn`` of UOX3's ``dfndata`` folder (``mobile_source``) into the file ``starting_items_destination``
    (``starting_items.toml``); ``items`` is what the item pass computed, which the starting items resolve against. Writes its report to
    ``output``; returns 0 when done, 2 for a bad source (nothing written)."""
    raise NotImplementedError("the UOX3 starting items pass is not ported yet")
