"""Converts ModernUO's world and dungeon teleporters (``Distribution/Data/teleporters.json``, placed by ``[TelGen``).

An entry is a source, a destination and ``back``, which adds the teleporter from the destination to the source. They are written to
``<map>/teleporters.toml`` for each map with teleporters, replacing those of a previous run.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import TextIO

from .textutil import is_json_int, load_loose_json, write_text

FILE_NAME = "teleporters.toml"
TELEPORTER_GRAPHIC = 0x1BC3

# [TelGen deletes the teleporters this close in height to a new one on the same cell.
SAME_SPOT_HEIGHT = 12

# ModernUO's map names and the decoration folder of each.
MAPS = [
    ("Felucca", "felucca"),
    ("Trammel", "trammel"),
    ("Ilshenar", "ilshenar"),
    ("Malas", "malas"),
    ("Tokuno", "tokuno"),
    ("TerMur", "termur"),
]

# A teleporter: x, y, z, the index of its destination's map, and the destination.
_Teleporter = tuple[int, int, int, int, int, int, int]
_Place = tuple[int, int, int, int]


def run(source: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    if not source.is_file():
        error.write(f"ModernUO teleporters.json does not exist: {source}\n")

        return 2

    teleporters: list[list[_Teleporter]] = [[] for _ in MAPS]

    try:
        document = load_loose_json(source)
    except (json.JSONDecodeError, UnicodeDecodeError) as exception:
        error.write(f"{source}: not valid JSON, or not a list of teleporters: {exception}\n")

        return 2

    if not isinstance(document, list):
        error.write(f"{source}: not valid JSON, or not a list of teleporters: the root is not a list\n")

        return 2

    for index, entry in enumerate(document, start=1):
        has_back = isinstance(entry, dict) and "back" in entry
        origin = _read_place(entry, "src")
        target = _read_place(entry, "dst")

        if origin is None or target is None or (has_back and not isinstance(entry["back"], bool)):
            error.write(f"{source}: entry {index} is not a teleporter: {json.dumps(entry)}\n")

            return 2

        _add(teleporters[origin[0]], origin, target)

        if has_back and entry["back"]:
            _add(teleporters[target[0]], target, origin)

    # An empty list would only delete the files of an earlier run.
    if all(not listed for listed in teleporters):
        error.write(f"{source}: no teleporters in it.\n")

        return 2

    for map_index, (_, folder) in enumerate(MAPS):
        path = destination / folder / FILE_NAME

        if not teleporters[map_index]:
            path.unlink(missing_ok=True)

            continue

        # One block per destination, in the order of its first teleporter.
        blocks: dict[tuple[int, int, int, int], list[_Teleporter]] = {}

        for teleporter in teleporters[map_index]:
            blocks.setdefault(teleporter[3:], []).append(teleporter)

        write_text(path, _write(map_index, blocks))
        output.write(f"{folder}/{FILE_NAME}: {len(teleporters[map_index])} teleporters in {len(blocks)} blocks\n")

    return 0


def _add(listed: list[_Teleporter], origin: _Place, target: _Place) -> None:
    _, x, y, z = origin
    listed[:] = [other for other in listed if not (other[0] == x and other[1] == y and abs(other[2] - z) <= SAME_SPOT_HEIGHT)]
    listed.append((x, y, z, *target))


def _read_place(entry: object, name: str) -> _Place | None:
    """The map index and the x, y, z of a ``src`` or ``dst``; None when it is not a place."""
    if not isinstance(entry, dict) or not isinstance(entry.get(name), dict):
        return None

    place = entry[name]
    map_name = place.get("map")
    location = place.get("loc")

    if not isinstance(map_name, str) or not isinstance(location, list) or len(location) != 3:
        return None

    names = [known for known, _ in MAPS]

    if map_name not in names or not all(is_json_int(part) for part in location):
        return None

    return names.index(map_name), location[0], location[1], location[2]


def _write(map_index: int, blocks: dict[tuple[int, int, int, int], list[_Teleporter]]) -> str:
    name, folder = MAPS[map_index]
    lines = [
        "# ==============================================================================\n"
        f"# Moongate - templates/decorations/{folder}/{FILE_NAME}\n"
        "#\n"
        "# What it is for:\n"
        f"#   The world and dungeon teleporters placed on {name}. Each block is one\n"
        "#   destination, with a teleporter at every location it lists.\n"
        "#\n"
        "# Fields:\n"
        "#   type       Teleporter\n"
        "#   item_id    the graphic, seen by staff only\n"
        "#   props      point_dest, [x, y, z] of the destination; map_dest, its map when\n"
        "#              it is another one\n"
        "#   locations  [x, y, z] of every teleporter of the block\n"
        "# ==============================================================================\n"
    ]

    for (dest_map, dest_x, dest_y, dest_z), locations in blocks.items():
        props = f"point_dest = [{dest_x}, {dest_y}, {dest_z}]"

        if dest_map != map_index:
            props += f', map_dest = "{MAPS[dest_map][0]}"'

        lines.append("\n[[decoration]]\n")
        lines.append('type = "Teleporter"\n')
        lines.append(f"item_id = 0x{TELEPORTER_GRAPHIC:04X}\n")
        lines.append(f"props = {{ {props} }}\n")

        if len(locations) == 1:
            lines.append(f"locations = [[{locations[0][0]}, {locations[0][1]}, {locations[0][2]}]]\n")

            continue

        lines.append("locations = [\n")
        lines.extend(f"    [{x}, {y}, {z}],\n" for x, y, z, *_ in locations)
        lines.append("]\n")

    return "".join(lines)
