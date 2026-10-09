"""Converts the named places of ModernUO's ``[Go`` gump (``Distribution/Data/Locations/<map>.json``, nested categories of places).

One data file comes out, ``locations.toml``: a ``[[location]]`` per place with its map, its categories joined by ``/``, its name and its
spot.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import TextIO

from .report import ConversionReport
from .textutil import is_json_int, load_loose_json, toml_escaped, write_text

CATEGORY_SEPARATOR = "/"

# The map files, in the order the places are written.
MAPS = ["felucca", "trammel", "ilshenar", "malas", "tokuno", "termur"]

_Place = tuple[str, str, str, int, int, int]

# Places that ModernUO's data gets wrong and the server needs right: (category, name, wrong spot) -> right spot. Cell 7 of the jail
# stands on the spot of Cell 6 in ModernUO, and the jail wants ten cells of its own (data/jail.toml).
CORRECTIONS: dict[tuple[str, str, tuple[int, int, int]], tuple[int, int, int]] = {
    ("Internal/Jail Cells", "Cell 7", (5286, 1174, 0)): (5296, 1174, 0),
}


def run(source: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    if not source.is_dir():
        error.write(f"ModernUO Locations folder does not exist: {source}\n")

        return 2

    places: list[_Place] = []
    report = ConversionReport()
    maps = 0

    for map_name in MAPS:
        path = source / f"{map_name}.json"

        if not path.is_file():
            continue

        maps += 1

        try:
            document = load_loose_json(path)
        except (json.JSONDecodeError, UnicodeDecodeError) as exception:
            error.write(f"{path}: not valid JSON: {exception}\n")

            return 2

        problem = _read(document, map_name, "", places)
        places[:] = [_corrected(place, report) for place in places]

        if problem is not None:
            error.write(f"{path}: {problem}\n")

            return 2

    if not places:
        error.write(f"{source}: no places in it; it must hold files such as felucca.json.\n")

        return 2

    write_text(destination, _write(places))
    output.write(f"{destination.name}: {len(places)} places on {maps} maps\n")
    report.write(output)

    return 0


def _corrected(place: _Place, report: ConversionReport) -> _Place:
    map_name, category, name, x, y, z = place
    right = CORRECTIONS.get((category, name, (x, y, z)))

    if right is None:
        return place

    report.count(f"{name} of '{category}' moved to {right}, as the server needs it")

    return map_name, category, name, *right


def _read(node: object, map_name: str, category: str, places: list[_Place]) -> str | None:
    """Reads the places of a category and of those inside it; a problem as text, or None."""
    if not isinstance(node, dict):
        return f"a category of '{category}' is not an object."

    locations = node.get("locations")

    if isinstance(locations, list):
        for place in locations:
            name = place.get("name") if isinstance(place, dict) else None

            if not isinstance(name, str) or not name.strip():
                return f"a place of '{category}' has no name."

            spot = place.get("location")

            if not isinstance(spot, list) or len(spot) != 3 or not all(is_json_int(part) for part in spot):
                return f"the place '{name}' has no location [x, y, z]."

            places.append((map_name, category, name.strip(), spot[0], spot[1], spot[2]))

    categories = node.get("categories")

    if not isinstance(categories, list):
        return None

    for child in categories:
        name = child.get("name") if isinstance(child, dict) else None

        if not isinstance(name, str) or not name.strip():
            return f"a category of '{category}' has no name."

        # The separator inside a name would split the category in two.
        segment = name.strip().replace(CATEGORY_SEPARATOR, "-")
        path = segment if not category else category + CATEGORY_SEPARATOR + segment
        problem = _read(child, map_name, path, places)

        if problem is not None:
            return problem

    return None


def _write(places: list[_Place]) -> str:
    lines = [
        "# ==============================================================================\n"
        "# Moongate - locations.toml\n"
        "#\n"
        "# What it is for:\n"
        '#   The named places staff travels to: ".go" opens a gump that lists them by\n'
        '#   map and category, and ".go <name>" goes to one. A place of a map the\n'
        "#   server does not load is left out.\n"
        "#\n"
        "# Fields:\n"
        "#   [[location]]  one per place, in the order the gump lists them\n"
        "#   map           felucca, trammel, ilshenar, malas, tokuno or termur\n"
        "#   category      where the gump files it: the categories from the map down,\n"
        '#                 joined by "/", such as "Dungeons/Covetous"; empty for a\n'
        "#                 place listed under the map itself\n"
        '#   name          the name shown, such as "Entrance"\n'
        '#   location      where the traveller arrives, "(x, y, z)"\n'
        "# ==============================================================================\n"
    ]

    for map_name, category, name, x, y, z in places:
        lines.append("\n[[location]]\n")
        lines.append(f'map = "{map_name}"\n')
        lines.append(f'category = "{toml_escaped(category)}"\n')
        lines.append(f'name = "{toml_escaped(name)}"\n')
        lines.append(f'location = "({x}, {y}, {z})"\n')

    return "".join(lines)
