"""Converts the treasure chests of ModernUO's spawners (``Distribution/Data/Spawns/<era>/<map>/*.json``).

The entries named ``TreasureChestLevel1`` to ``4`` become spawn regions of items, one per spawner with chests, written as
``<map>/treasure_chests.toml`` and replacing that of a previous run. The creatures of the same spawners are left to the spawn converter.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import TextIO

from .report import ConversionReport
from .spawn import MAP_NAMES, Spawn, folder_of, map_of, minutes, place, serialize
from .textutil import is_json_int, load_loose_json, snake_case, write_text

FILE_NAME = "treasure_chests.toml"
CLASS_PREFIX = "TreasureChestLevel"
LEVELS = 4

# The eras of ModernUO's spawns that make the world of a modern client.
ERAS = ["shared", "post-uoml"]


def run(source: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    if not source.is_dir():
        error.write(f"ModernUO spawns folder does not exist: {source}\n")

        return 2

    report = ConversionReport()
    by_map: dict[str, list[Spawn]] = {}

    for map_name in MAP_NAMES:
        # The folder name of both ModernUO and Moongate: termur, not ter_mur.
        folder = folder_of(map_name)

        for era in ERAS:
            directory = source / era / folder

            if not directory.is_dir():
                continue

            for file in sorted(directory.glob("*.json"), key=lambda path: path.name):
                stem = snake_case(file.stem)

                try:
                    spawners = load_loose_json(file)
                except (json.JSONDecodeError, UnicodeDecodeError) as exception:
                    error.write(f"{file}: not valid JSON: {exception}\n")

                    return 2

                if not isinstance(spawners, list):
                    error.write(f"{file}: not a list of spawners\n")

                    return 2

                for index, spawner in enumerate(spawners):
                    if not _is_spawner(spawner):
                        error.write(f"{file}: spawner {index + 1} is not a spawner\n")

                        return 2

                    spawn_id = f"{folder}_chest_{snake_case(era)}_{stem}_{index}"
                    chests = _build(spawner, spawn_id, map_name, report)

                    if chests is not None:
                        by_map.setdefault(folder, []).append(chests)

    if not by_map:
        error.write(f"{source}: no treasure chest in its spawners; it must hold folders such as shared/felucca.\n")

        return 2

    for folder, spawns in sorted(by_map.items()):
        path = destination / folder / FILE_NAME
        write_text(path, serialize(spawns))
        output.write(f"spawns/{folder}/{FILE_NAME} ({len(spawns)} chest region(s))\n")

    report.write(output)
    output.write(f"Wrote {sum(len(spawns) for spawns in by_map.values())} chest region(s) from ModernUO's spawners.\n")

    return 0


def _is_spawner(spawner: object) -> bool:
    """A spawner has a location of three whole numbers, a list of entries and, when it has one, a whole count."""
    if not isinstance(spawner, dict) or not isinstance(spawner.get("entries"), list):
        return False

    location = spawner.get("location")
    count = spawner.get("count", 1)

    return (
        isinstance(location, list)
        and len(location) >= 3
        and all(is_json_int(part) for part in location[:3])
        and is_json_int(count)
        and all(isinstance(entry, dict) for entry in spawner["entries"])
    )


def _build(spawner: dict, spawn_id: str, folder_map: str, report: ConversionReport) -> Spawn | None:
    """One region for the chests of a spawner: it picks among their levels, and as many live at once as their caps allow."""
    count = max(1, spawner.get("count", 1))
    levels: set[int] = set()
    caps = 0

    for entry in spawner["entries"]:
        name = entry.get("name") or ""

        if not name.lower().startswith(CLASS_PREFIX.lower()):
            continue

        digits = name[len(CLASS_PREFIX) :]
        level = int(digits) if re.fullmatch(r"[0-9]+", digits) else None

        if level is None or not 1 <= level <= LEVELS:
            report.count(f"unknown chest {name}")

            continue

        levels.add(level)
        caps += min(max(entry.get("maxCount", count), 1), count)

    if not levels:
        return None

    low = minutes(spawner, "minDelay")
    ordered = sorted(levels)
    spawn = Spawn(
        id=spawn_id,
        map=map_of(spawner, folder_map),
        name=f"Treasure chest level {', '.join(str(level) for level in ordered)}",
        item_ids=[f"treasure_chest_level_{level}" for level in ordered],
        max=min(count, caps),
        min_minutes=low,
        max_minutes=max(low, minutes(spawner, "maxDelay")),
    )
    place(spawner, spawn)

    return spawn
