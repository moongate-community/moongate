"""What the item pass computed that the mobile pass resolves against, and how a UOX3 item reference is resolved to template ids."""

from __future__ import annotations

from dataclasses import dataclass, field

from .dfn import DfnBlock, IgnoreCaseDict, IgnoreCaseSet, parent_targets
from .report import ConversionReport
from .textutil import trim, try_int

LIST_PREFIX = "listobject"


@dataclass
class ItemIndex:
    """Every item block's template id by header, every item-source block by header (for ``[ITEMLIST n]``), and every loot table id."""

    item_id_by_header: IgnoreCaseDict[str]
    item_blocks_by_header: IgnoreCaseDict[DfnBlock]
    loot_ids: IgnoreCaseSet
    item_ids: set[str] = field(init=False)

    def __post_init__(self) -> None:
        self.item_ids = set(self.item_id_by_header.values())


def list_number(value: str) -> int | None:
    """The ``[ITEMLIST n]`` number of a ``listobjectN`` reference; None for any other reference."""
    if value[: len(LIST_PREFIX)].lower() != LIST_PREFIX:
        return None

    return try_int(value[len(LIST_PREFIX) :])


def resolve(value: str, items: ItemIndex, report: ConversionReport) -> list[str]:
    """The ids a reference (``0x1f03``, ``bagofreagents``, ``listobject6``) can give, one picked at random; none, and counted, when nothing
    resolves."""
    number = list_number(value)

    if number is not None:
        listing = items.item_blocks_by_header.get(f"ITEMLIST {number}")

        if listing is None:
            report.count("unresolved item list")

            return []

        # Lines are "weight|item" or "item"; "blank" is a chance of nothing. Items is an even pick, so the weights and the blanks are dropped.
        headers: list[str] = []

        for line in listing.entries:
            item = trim(line.split(" ", 1)[0])
            bar = item.find("|")

            if bar >= 0:
                item = item[bar + 1 :]
                report.count("item list weight or blank dropped")

            if item.lower() == "blank":
                if bar < 0:
                    report.count("item list weight or blank dropped")

                continue

            headers.append(item)
    else:
        headers = [value]

    ids: list[str] = []

    for header in headers:
        resolved = _resolve_ids(header, items, 0)

        if not resolved:
            report.count("unresolved item")

        for item_id in resolved:
            if item_id not in ids:
                ids.append(item_id)

    return ids


def _resolve_ids(header: str, items: ItemIndex, depth: int) -> list[str]:
    """An item block without an id of its own is an alias: ``getlbr=x`` is x in UOX3's default era, ``get=a b`` is a or b. Items already
    picks one evenly, so a random get becomes every target."""
    item_id = items.item_id_by_header.get(header)

    if item_id is not None:
        return [item_id]

    block = items.item_blocks_by_header.get(header)

    if depth > 8 or block is None:
        return []

    found: list[str] = []

    for target in parent_targets(block):
        for resolved in _resolve_ids(target, items, depth + 1):
            if resolved not in found:
                found.append(resolved)

    return found
