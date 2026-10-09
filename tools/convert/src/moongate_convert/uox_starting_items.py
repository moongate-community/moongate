"""UOX3's starting items (``newbie/newbie.dfn``) as ``starting_items.toml`` (the starting items pass of ``uox``).

The ``[BESTSKILL n]`` and ``[DEFAULT ...]`` blocks become sets, and the gold, the bread and the water of Moongate itself join the common set.
The file is read back as the server will read it, and every item it names is checked.
"""

from __future__ import annotations

import tomllib
from dataclasses import dataclass, field
from pathlib import Path
from typing import TextIO

from . import dfn, item_index, tomlout
from .dfn import DfnBlock
from .guildmasters import SKILL_TYPES
from .item_index import ItemIndex
from .report import ConversionReport
from .specs import MAX_HUE
from .textutil import read_lines, snake_case, trim, try_int, write_text

_HEADER = """\
# ==============================================================================
# Moongate - starting_items.toml
#
# What it is for:
#   The items a new character gets: every common set, plus every set whose
#   filters it matches. The starting gold, the bread and the water are items
#   of the common set.
#
# Fields of a [[set]]:
#   common  true gives the set to every character
#   skill   given to characters starting with this skill among their best ones
#   race    human, elf or gargoyle; unset is every race
#   gender  male or female; unset is both
#
# Fields of a [[set.items]]:
#   items   item template ids; one is picked at random
#   amount  how many, as dice; unset is 1
#   hue     the hue to give the item; unset keeps its own
#   equip   true puts it on the character, false in the backpack
#   newbie  whether it stays on death; unset is the server's default
# ==============================================================================

"""

_SKILL_PREFIX = "BESTSKILL "
_COMMON_HEADER = "DEFAULT ALL"

# What UOX3 does not keep in newbie.dfn and every new character of Moongate gets: the starting gold, which UOX3 reads from uox.ini
# (STARTGOLD), and something against hunger and thirst.
_GOLD_HEADER = "0x0eed"
_STARTING_GOLD = 1000
_BREAD_HEADER = "0x103b"
_STARTING_BREAD = 3
_WATER_HEADER = "0x1f9e"

# UOX3 picks the DEFAULT section by body: MALE and FEMALE are the human bodies only.
_DEFAULTS = {
    "default male": ("human", "male"),
    "default female": ("human", "female"),
    "default elf male": ("elf", "male"),
    "default elf female": ("elf", "female"),
    "default garg male": ("gargoyle", "male"),
    "default garg female": ("gargoyle", "female"),
}


@dataclass
class _Entry:
    items: list[str]
    amount: int | None = None
    hue: int | None = None
    equip: bool = False
    newbie: bool | None = None


@dataclass
class _Set:
    common: bool = False
    skill: str | None = None
    race: str | None = None
    gender: str | None = None
    items: list[_Entry] = field(default_factory=list)


def run(mobile_source: Path, starting_items_destination: Path, items: ItemIndex, output: TextIO, error: TextIO) -> int:
    """Converts ``newbie/newbie.dfn`` of UOX3's ``dfndata`` folder (``mobile_source``) into the file ``starting_items_destination``
    (``starting_items.toml``); ``items`` is what the item pass computed, which the starting items resolve against. Writes its report to
    ``output``; returns 0 when done, 1 when reading the file back finds an item that does not exist, 2 for a bad source (nothing written)."""
    source = mobile_source / "newbie" / "newbie.dfn"

    if not source.is_file():
        error.write(f"Starting items source does not exist: {source}\n")

        return 2

    report = ConversionReport()
    sets = build(dfn.parse(read_lines(source)), items, report)

    write_text(starting_items_destination, _HEADER + _serialize(sets))
    output.write(f"Converted {len(sets)} starting item set(s) to {starting_items_destination}.\n")
    report.write(output)

    # Read back as the server will, and check every item exists.
    written = tomllib.loads(starting_items_destination.read_text(encoding="utf-8-sig")).get("set", [])
    errors = [
        f"starting item '{item}' does not exist"
        for entry_set in written
        for entry in entry_set.get("items", [])
        for item in entry.get("items", [])
        if item not in items.item_ids
    ]

    if errors:
        for message in errors:
            error.write(f"Verification failed: {message}\n")

        error.write(f"{len(errors)} verification error(s) found reading the converted starting items back.\n")

        return 1

    output.write(f"Verified {len(written)} starting item set(s) read back from disk: every item resolves.\n")

    return 0


def build(blocks: list[DfnBlock], items: ItemIndex, report: ConversionReport) -> list[_Set]:
    """One set per block that gives at least one item, in the order of the blocks, then Moongate's own gold, bread and water."""
    sets: list[_Set] = []

    for block in blocks:
        header = block.header.lower()

        if header == _COMMON_HEADER.lower():
            entry_set = _Set(common=True)
        elif header in _DEFAULTS:
            entry_set = _Set(race=_DEFAULTS[header][0], gender=_DEFAULTS[header][1])
        elif header.startswith(_SKILL_PREFIX.lower()) and (skill := try_int(trim(block.header[len(_SKILL_PREFIX) :]))) is not None:
            # SkillType is a byte: a number over 255 wraps, as the cast does.
            if skill & 0xFF >= len(SKILL_TYPES):
                continue

            entry_set = _Set(skill=_from_uox_skill_number(skill))
        else:
            continue

        for line in block.entries:
            entry = _build_entry(line, items, report)

            if entry is not None:
                entry_set.items.append(entry)

        if entry_set.items:
            sets.append(entry_set)

    _add_own_items(sets, items)

    return sets


def _add_own_items(sets: list[_Set], items: ItemIndex) -> None:
    """Moongate's own entries of the common set, for the items the source has: the gold first, food and drink last."""
    gold = _own_entry(items, _GOLD_HEADER, _STARTING_GOLD)
    food = [entry for entry in (_own_entry(items, _BREAD_HEADER, _STARTING_BREAD), _own_entry(items, _WATER_HEADER, 1)) if entry is not None]

    if gold is None and not food:
        return

    common = next((entry_set for entry_set in sets if entry_set.common), None)

    if common is None:
        common = _Set(common=True)
        sets.append(common)

    if gold is not None:
        common.items.insert(0, gold)

    common.items.extend(food)


def _own_entry(items: ItemIndex, header: str, amount: int) -> _Entry | None:
    item_id = items.item_id_by_header.get(header)

    if item_id is None:
        return None

    return _Entry([item_id], None if amount == 1 else amount)


def _build_entry(line: str, items: ItemIndex, report: ConversionReport) -> _Entry | None:
    """``PACKITEM=item[,amount[,newbie]]`` and ``EQUIPITEM=item[,hue[,newbie]]``."""
    separator = line.find("=")

    if separator < 0:
        return None

    key = trim(line[:separator]).upper()

    if key not in ("PACKITEM", "EQUIPITEM"):
        report.count(f"unknown newbie tag {key}")

        return None

    parts = [trim(part) for part in line[separator + 1 :].split(",")]
    ids = item_index.resolve(parts[0], items, report)

    if not ids:
        return None

    entry = _Entry(ids, equip=key == "EQUIPITEM")

    if len(parts) >= 2 and (value := dfn.uox_number(parts[1])) is not None:
        if entry.equip:
            # HueSpec.FromValue refuses a number that is no hue.
            if not 0 <= value <= MAX_HUE:
                raise ValueError(f"{value} is not a hue: {line}")

            entry.hue = value
        elif value != 1:
            entry.amount = value

    if len(parts) >= 3 and (newbie := dfn.uox_number(parts[2])) is not None:
        entry.newbie = newbie != 0

    return entry


def _from_uox_skill_number(number: int) -> str:
    """UOX3 numbers IMBUING 55 and MYSTICISM 56 (enums.h); ``SkillType`` has them the other way round. Every other number matches."""
    if number == 55:
        return "Imbuing"

    if number == 56:
        return "Mysticism"

    return SKILL_TYPES[number & 0xFF]


def _serialize(sets: list[_Set]) -> str:
    """The sets as the ``[[set]]`` tables Tomlyn writes: a blank line before every table after the first of its array, and the empty
    ``book_values`` table of each entry."""
    if not sets:
        return "set = []\n"

    blocks = []

    for entry_set in sets:
        lines = ["[[set]]", f"common = {'true' if entry_set.common else 'false'}"]

        if entry_set.skill is not None:
            lines.append(f"skill = {tomlout.basic(snake_case(entry_set.skill))}")

        if entry_set.race is not None:
            lines.append(f"race = {tomlout.basic(entry_set.race)}")

        if entry_set.gender is not None:
            lines.append(f"gender = {tomlout.basic(entry_set.gender)}")

        entries = []

        for entry in entry_set.items:
            entry_lines = ["[[set.items]]", f"items = {tomlout.strings(entry.items)}"]

            if entry.amount is not None:
                entry_lines.append(f"amount = {entry.amount}")

            if entry.hue is not None:
                entry_lines.append(f"hue = {entry.hue}")

            entry_lines.append(f"equip = {'true' if entry.equip else 'false'}")

            if entry.newbie is not None:
                entry_lines.append(f"newbie = {'true' if entry.newbie else 'false'}")

            entry_lines.append("[set.items.book_values]")
            entries.append("\n".join(entry_lines) + "\n")

        blocks.append("\n".join(lines) + "\n" + "\n".join(entries))

    return "\n".join(blocks)
