"""UOX3's ``.dfn`` block format, and how the rest of the UOX3 converter reads a block and a number.

A ``.dfn`` file is ``// comment`` lines, blank lines, a ``[header]`` line, a bare ``{``, one line per entry and a bare ``}``. A trailing
``//comment`` is taken off every line first, as UOX3's own ``ssection.cpp`` does: real data has it glued onto a brace (``{//approximately
1%``), which would otherwise hide the whole block.
"""

from __future__ import annotations

import re
from collections.abc import Iterable, Iterator, Mapping, MutableMapping
from dataclasses import dataclass, field

from .textutil import WHITESPACE, trim


class IgnoreCaseDict[V](MutableMapping[str, V]):
    """A dictionary whose keys are the same whatever their case, keeping the spelling that came first (``OrdinalIgnoreCase``)."""

    def __init__(self, source: Mapping[str, V] | None = None) -> None:
        self._values: dict[str, tuple[str, V]] = {}

        for key, value in (source or {}).items():
            self[key] = value

    def __getitem__(self, key: str) -> V:
        return self._values[key.lower()][1]

    def __setitem__(self, key: str, value: V) -> None:
        folded = key.lower()
        self._values[folded] = (self._values[folded][0] if folded in self._values else key, value)

    def __delitem__(self, key: str) -> None:
        del self._values[key.lower()]

    def __iter__(self) -> Iterator[str]:
        return (spelling for spelling, _ in self._values.values())

    def __len__(self) -> int:
        return len(self._values)

    def __contains__(self, key: object) -> bool:
        return isinstance(key, str) and key.lower() in self._values

    def copy(self) -> IgnoreCaseDict[V]:
        return IgnoreCaseDict(self)


class IgnoreCaseSet:
    """A set of strings that are the same whatever their case (``HashSet<string>`` with ``OrdinalIgnoreCase``)."""

    def __init__(self, values: Iterable[str] = ()) -> None:
        self._values = {value.lower() for value in values}

    def add(self, value: str) -> None:
        self._values.add(value.lower())

    def discard(self, value: str) -> None:
        self._values.discard(value.lower())

    def __contains__(self, value: object) -> bool:
        return isinstance(value, str) and value.lower() in self._values

    def __len__(self) -> int:
        return len(self._values)


@dataclass(eq=False)
class DfnBlock:
    """One ``[header] { ... }`` block of a ``.dfn`` file; it is compared by identity.

    ``fields`` holds the ``key=value`` lines of an item block, ``entries`` every line verbatim (what a ``[LOOTLIST ...]`` block's bare entry
    lines need), ``comments`` the ``//`` comment of each field's line and ``entry_comments`` the comment of every line, in step with
    ``entries``: UOX3 writes ``NAME=#//an orc`` and ``3009//a daemon``, where the comment is the only readable text. ``label`` is the text
    after the opening brace, such as ``{ Human Male``.
    """

    header: str
    fields: IgnoreCaseDict[str]
    entries: list[str]
    comments: IgnoreCaseDict[str]
    label: str | None
    entry_comments: list[str | None] = field(default_factory=list)


def parse(lines: list[str]) -> list[DfnBlock]:
    """The blocks of a ``.dfn`` file, from its lines."""
    blocks: list[DfnBlock] = []
    header: str | None = None
    fields: IgnoreCaseDict[str] | None = None
    entries: list[str] | None = None
    comments: IgnoreCaseDict[str] = IgnoreCaseDict()
    entry_comments: list[str | None] = []
    label: str | None = None

    for raw_line in lines:
        line = trim(raw_line)
        comment_index = line.find("//")
        comment: str | None = None

        if comment_index >= 0:
            comment = trim(line[comment_index + 2 :]) or None
            line = line[:comment_index].rstrip(WHITESPACE)

        if not line:
            continue

        if line.startswith("[") and line.endswith("]"):
            header = line[1:-1]
            fields = None
            entries = None

            continue

        # "{ Human Male" opens the block too; the text after the brace is a label, not an entry.
        if line.startswith("{"):
            fields = IgnoreCaseDict()
            entries = []
            comments = IgnoreCaseDict()
            entry_comments = []
            label = trim(line[1:]) or None

            continue

        if line == "}":
            if header is not None and fields is not None and entries is not None:
                blocks.append(DfnBlock(header, fields, entries, comments, label, entry_comments))

            header = None
            fields = None
            entries = None

            continue

        if fields is None or entries is None:
            continue

        entries.append(line)
        entry_comments.append(comment)
        separator = line.find("=")

        if separator < 0:
            continue

        key = trim(line[:separator])
        value = trim(line[separator + 1 :])

        # UOX3 skips a tag with no value (ssection.cpp), such as baseitem.dfn's bare decay= and pileable=.
        if not value:
            continue

        fields[key] = value

        if comment is not None:
            comments[key] = comment

    return blocks


def get_targets(block: DfnBlock) -> list[str]:
    """The ``get=`` targets of a block, none when it has none; two or more are a random pick."""
    text = block.fields.get("get")

    if text is None:
        return []

    return [target for part in text.split(" ") if (target := trim(part))]


def parent_targets(block: DfnBlock) -> list[str]:
    """The targets a block inherits from: its ``getlbr=`` when it has one (LBR is UOX3's default era and the era line comes after ``get=``),
    else its ``get=`` targets. The other era tags are ignored."""
    era_target = block.fields.get("getlbr")

    return [trim(era_target)] if era_target is not None else get_targets(block)


_HEX_DIGITS = re.compile(r"[0-9a-fA-F]+", re.ASCII)
_DECIMAL = re.compile(r"[+-]?[0-9]+", re.ASCII)
INT32_MIN = -(2**31)
INT32_MAX = 2**31 - 1


def uox_number(text: str) -> int | None:
    """A UOX3 number as UOX3 reads it (``stoi(value, nullptr, 0)``): hex with ``0x`` or decimal, from the first token. It forgives the typos
    real UOX3 data has: a doubled prefix (``0x0x04FC``) and trailing punctuation (``0x15b6]``). None when it is no number."""
    text = trim(text)
    stops = [index for index in (text.find(" "), text.find("\t")) if index >= 0]

    if stops:
        text = text[: min(stops)]

    text = text.rstrip("],;")

    if text[:4].lower() == "0x0x":
        text = text[2:]

    if text[:2].lower() == "0x":
        digits = text[2:]

        if not _HEX_DIGITS.fullmatch(digits):
            return None

        # int.TryParse with hex digits reads the bits of a 32 bit number: ffffffff is -1.
        value = int(digits, 16)

        if value > 0xFFFFFFFF:
            return None

        return value - 2**32 if value > INT32_MAX else value

    if not _DECIMAL.fullmatch(text):
        return None

    value = int(text)

    return value if INT32_MIN <= value <= INT32_MAX else None
