"""The converters keep lists the server also keeps (the maps); these tests read the C# files and fail when the two drift."""

from __future__ import annotations

import re
from pathlib import Path

from moongate_convert import locations, spawn, teleporters

REPOSITORY = Path(__file__).resolve().parents[3]


def enum_members(relative: str) -> list[str]:
    """The member names of the first enum of a C# file, in order."""
    text = (REPOSITORY / relative).read_text(encoding="utf-8")
    body = text[text.index("{", text.index("enum ")) + 1 :]
    body = body[: body.index("}")]
    body = re.sub(r"///.*", "", body)

    return re.findall(r"^\s*([A-Za-z]\w*)\s*(?:=\s*\d+)?\s*,?\s*$", body, flags=re.MULTILINE)


def test_the_maps_are_those_of_the_server():
    maps = enum_members("src/Moongate.Ultima/Types/MapType.cs")

    assert spawn.MAP_NAMES == maps
    assert [name for name, _ in teleporters.MAPS] == maps
    assert locations.MAPS == [name.lower() for name in maps]
