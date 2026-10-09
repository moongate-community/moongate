"""What the tests of the UOX3 passes that follow the item pass share: the item index of a source folder."""

from __future__ import annotations

import io
from pathlib import Path

from moongate_convert import uox
from moongate_convert.dfn import IgnoreCaseDict, IgnoreCaseSet
from moongate_convert.item_index import ItemIndex


def item_index(source: Path) -> ItemIndex:
    """What the item pass computes from a folder of item ``.dfn`` files, as ``uox.run`` does before the mobile pass."""
    files = [source] if source.is_file() else sorted((path for path in source.rglob("*.dfn") if path.is_file()), key=str)
    blocks_by_file, blocks_by_header = uox._read_blocks(files, io.StringIO())
    uox._flatten_blocks(blocks_by_file, blocks_by_header)
    id_by_header: IgnoreCaseDict[str] = IgnoreCaseDict()
    loot_ids = IgnoreCaseSet()
    uox._compute_ids(blocks_by_header, True, id_by_header, {}, IgnoreCaseDict(), loot_ids)

    return ItemIndex(id_by_header, blocks_by_header, loot_ids)
