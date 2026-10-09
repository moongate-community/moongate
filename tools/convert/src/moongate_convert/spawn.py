"""What the ModernUO spawn and chest converters share: the spawn region, the placing of a spawner, the delays, the map of a spawner.

A region is written the way the C# ``SpawnTemplate`` is serialized, so that the loaders read it back the same.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field

from .textutil import snake_case, toml_escaped

# How far above its spawner a spot may be, so a spawner in a cave does not spawn on the hill over it.
HEADROOM = 16

# The maps of the server, in the order of ``MapType``, with the name the data files use for each.
MAP_NAMES = ["Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "TerMur"]


def map_key(name: str) -> str:
    """The name of a map in the TOML files: ``felucca``, ``ter_mur``."""
    return snake_case(name)


def folder_of(name: str) -> str:
    """The folder name of both ModernUO and Moongate: ``termur``, not ``ter_mur``."""
    return name.lower()


@dataclass
class SpawnArea:
    x1: int = 0
    y1: int = 0
    x2: int = 0
    y2: int = 0


@dataclass
class Spawn:
    id: str
    map: str
    name: str | None = None
    mobile_ids: list[str] = field(default_factory=list)
    npc_list_ids: list[str] = field(default_factory=list)
    item_ids: list[str] = field(default_factory=list)
    max: int = 1
    min_minutes: int = 0
    max_minutes: int = 0
    call: int = 1
    areas: list[SpawnArea] = field(default_factory=list)
    exclude: list[SpawnArea] = field(default_factory=list)
    pref_z: int | None = None
    z: int | None = None
    only_outside: bool = False


def map_of(spawner: dict, folder: str) -> str:
    """The map a spawner names, which is not always its folder's. One that names none, or an unknown one, takes the folder's."""
    value = spawner.get("map")

    if isinstance(value, str):
        for known in MAP_NAMES:
            if known.lower() == value.lower():
                return known

    return folder


def minutes(spawner: dict, key: str) -> int:
    """A ModernUO delay in whole minutes, a minute at least; a delay that is no time span is a minute."""
    value = spawner.get(key)
    parsed = _time_span_minutes(value) if isinstance(value, str) else None

    return 1 if parsed is None else max(1, parsed)


_DAYS = r"(?P<days>\d+)"
_NUMBER = r"\d+"
_SPAN = re.compile(
    rf"^(?:{_DAYS}|(?:(?P<d>\d+)\.)?(?P<a>{_NUMBER}):(?P<b>{_NUMBER})(?::(?P<c>{_NUMBER})(?:\.\d{{1,7}})?)?"
    rf"|(?P<d4>\d+):(?P<h4>{_NUMBER}):(?P<m4>{_NUMBER}):(?P<s4>{_NUMBER})(?:\.\d{{1,7}})?)$",
    re.ASCII,
)


def _time_span_minutes(text: str) -> int | None:
    """The whole minutes of a .NET ``TimeSpan`` text (``d``, ``[d.]hh:mm[:ss]``, ``d:hh:mm:ss``), or None when .NET rejects it."""
    match = _SPAN.match(text.strip(" \t\n\v\f\r"))

    if match is None:
        return None

    groups = match.groupdict()

    if groups["days"] is not None:
        return int(groups["days"]) * 1440

    if groups["d4"] is not None:
        days, hours, mins, secs = (int(groups[name]) for name in ("d4", "h4", "m4", "s4"))
    else:
        days = int(groups["d"] or 0)
        first, second, third = int(groups["a"]), int(groups["b"]), groups["c"]

        if third is not None and groups["d"] is None and first > 23:
            # .NET reads a first number over 23 as days: d:hh:mm.
            days, hours, mins, secs = first, second, int(third), 0
        else:
            hours, mins, secs = first, second, int(third or 0)

    if hours > 23 or mins > 59 or secs > 59:
        return None

    return days * 1440 + hours * 60 + mins


def place(spawner: dict, spawn: Spawn) -> None:
    """Gives the region its area and its height from the spawner."""
    x, y, z = spawner["location"][:3]
    bounds = spawner.get("spawnBounds")

    if isinstance(bounds, dict):
        start, end = bounds["start"], bounds["end"]
        spawn.areas.append(SpawnArea(max(0, start["x"]), max(0, start["y"]), end["x"], end["y"]))
        spawn.z = end["z"]

        return

    # Without a home range ModernUO spawns on the spawner's own spot.
    reach = max(0, spawner.get("homeRange", 0) or 0)
    spawn.areas.append(SpawnArea(max(0, x - reach), max(0, y - reach), x + reach, y + reach))
    spawn.z = z + HEADROOM


def serialize(spawns: list[Spawn]) -> str:
    """The regions as the ``[[spawn]]`` tables the C# serializer writes."""
    blocks = []

    for spawn in spawns:
        lines = ["[[spawn]]", f'id = "{toml_escaped(spawn.id)}"', f'map = "{map_key(spawn.map)}"']

        if spawn.name is not None:
            lines.append(f'name = "{toml_escaped(spawn.name)}"')

        lines.append(f"mobile_ids = {_strings(spawn.mobile_ids)}")
        lines.append(f"npc_list_ids = {_strings(spawn.npc_list_ids)}")
        lines.append(f"item_ids = {_strings(spawn.item_ids)}")
        lines.append(f"max = {spawn.max}")
        lines.append(f"min_minutes = {spawn.min_minutes}")
        lines.append(f"max_minutes = {spawn.max_minutes}")
        lines.append(f"call = {spawn.call}")

        if not spawn.exclude:
            lines.append("exclude = []")

        if not spawn.areas:
            lines.append("areas = []")

        if spawn.pref_z is not None:
            lines.append(f"pref_z = {spawn.pref_z}")

        if spawn.z is not None:
            lines.append(f"z = {spawn.z}")

        lines.append(f"only_outside = {'true' if spawn.only_outside else 'false'}")

        # A blank line comes before every table of an array after its first.
        for table, areas in (("spawn.areas", spawn.areas), ("spawn.exclude", spawn.exclude)):
            for index, area in enumerate(areas):
                lines.extend(["" if index > 0 else None, f"[[{table}]]", f"x1 = {area.x1}", f"y1 = {area.y1}", f"x2 = {area.x2}", f"y2 = {area.y2}"])

        lines = [line for line in lines if line is not None]

        blocks.append("\n".join(lines) + "\n")

    return "\n".join(blocks)


def _strings(values: list[str]) -> str:
    return "[" + ", ".join(f'"{toml_escaped(value)}"' for value in values) + "]"
