"""Converts ModernUO's spawners (``Distribution/Data/Spawns/<era>/<map>/*.json``) of the chosen maps into spawn regions.

It is for the maps UOX3 has no spawns for. It reads the ``shared`` and ``post-uoml`` eras, the world of a modern client, and writes
``<map>/modernuo_<file>.toml``, replacing the ``modernuo_`` files of the maps it converts. A region keeps the map its spawner names, which
may differ from its folder's, and goes into that map's folder, where the server looks for it. A region id names the era, the file and the
spawner's index in it, so it stays the same when a later run resolves more mobiles.
"""

from __future__ import annotations

import json
import math
import tomllib
from pathlib import Path
from typing import TextIO

from . import mobile_names
from .chests import ERAS
from .report import ConversionReport
from .spawn import MAP_NAMES, Spawn, folder_of, map_of, minutes, place, serialize
from .textutil import is_json_int, snake_case, write_text

FILE_PREFIX = "modernuo_"

# Classes that the town spawns of UOX3 place already, which a run with "only" would place a second time.
PLACED_BY_UOX3 = {"ThiefGuildmaster"}


def run(
    source: Path,
    maps: list[str],
    mobile_destination: Path,
    spawns_destination: Path,
    output: TextIO,
    error: TextIO,
    only: str | None = None,
) -> int:
    if not source.is_dir():
        error.write(f"ModernUO spawns folder does not exist: {source}\n")

        return 2

    try:
        ids = _read_ids(mobile_destination, "mobile")
        # The npc lists beside the mobiles name a class too, such as a guildmaster of either sex: a region lists them as lists.
        lists = _read_ids(mobile_destination.parent / "npc_lists", "npc_list")
    except (tomllib.TOMLDecodeError, UnicodeDecodeError, OSError) as exception:
        error.write(f"Cannot read the templates: {exception}\n")

        return 2

    # Without templates every spawner would be skipped and the shipped files replaced by nothing.
    if not ids:
        error.write(f"Found no mobile templates under {mobile_destination}.\n")

        return 2

    # A name that is both a mobile and a list (healer) stays a mobile.
    lists -= ids
    ids |= lists

    flat_ids: dict[str, str] = {}

    for template in sorted(ids):
        flat_ids.setdefault(mobile_names.flat(template), template)

    report = ConversionReport()
    converted: list[str] = []

    # By the map each region names, then by file: the Yomotsu Mines lie in the tokuno folder and on Malas, and the loader takes a
    # region's map from its folder.
    by_map: dict[str, dict[str, list[Spawn]]] = {}

    for map_name in maps:
        try:
            by_file = _convert_map(source, map_name, ids, lists, flat_ids, report, only)
        except _BadSource as exception:
            error.write(f"{exception}\n")

            return 2

        # Nothing to write keeps what a previous run wrote.
        if all(not spawns for spawns in by_file.values()):
            continue

        converted.append(map_name)

        for stem in sorted(by_file):
            for spawn in by_file[stem]:
                by_map.setdefault(spawn.map, {}).setdefault(stem, []).append(spawn)

    # Every old file first, then every new one: a map converted later must not delete what an earlier one moved into its folder.
    pattern = f"{FILE_PREFIX}*.toml" if only is None else f"{FILE_PREFIX}{snake_case(only)}s.toml"

    for map_name in converted:
        directory = spawns_destination / folder_of(map_name)

        if directory.is_dir():
            for old in sorted(directory.glob(pattern)):
                if old.is_file():
                    old.unlink()

    written = 0

    for map_name in sorted(by_map, key=MAP_NAMES.index):
        folder = folder_of(map_name)

        for stem in sorted(by_map[map_name]):
            spawns = by_map[map_name][stem]
            name = f"{FILE_PREFIX}{stem}.toml"
            write_text(spawns_destination / folder / name, serialize(spawns))
            written += len(spawns)
            output.write(f"spawns/{folder}/{name} ({len(spawns)} spawn region(s))\n")

    report.write(output)
    output.write(f"Wrote {written} spawn region(s) from ModernUO's spawners.\n")

    return 0


class _BadSource(Exception):
    """A spawner file that is not what ModernUO writes."""


def _read_ids(folder: Path, table: str) -> set[str]:
    """The ids of the ``[[<table>]]`` entries of every toml file under a folder; none when it does not exist."""
    ids: set[str] = set()

    if not folder.is_dir():
        return ids

    for file in sorted(folder.rglob("*.toml")):
        for entry in tomllib.loads(file.read_text(encoding="utf-8-sig")).get(table, []):
            ids.add(entry["id"])

    return ids


def _convert_map(
    source: Path,
    map_name: str,
    ids: set[str],
    lists: set[str],
    flat_ids: dict[str, str],
    report: ConversionReport,
    only: str | None,
) -> dict[str, list[Spawn]]:
    """The regions of a map's ModernUO folders, by file."""
    folder = folder_of(map_name)
    by_file: dict[str, list[Spawn]] = {}

    for era in ERAS:
        directory = source / era / folder

        if not directory.is_dir():
            continue

        for file in sorted(directory.glob("*.json"), key=lambda path: path.name):
            stem = snake_case(file.stem)
            key = stem if only is None else snake_case(only) + "s"
            spawns = by_file.setdefault(key, [])

            try:
                spawners = json.loads(file.read_text(encoding="utf-8-sig"))
            except (json.JSONDecodeError, UnicodeDecodeError) as exception:
                raise _BadSource(f"{file}: not valid JSON: {exception}") from exception

            if not isinstance(spawners, list):
                raise _BadSource(f"{file}: not a list of spawners")

            for index, spawner in enumerate(spawners):
                if not _is_spawner(spawner):
                    raise _BadSource(f"{file}: spawner {index + 1} is not a spawner")

                spawn_id = f"{folder}_modernuo_{snake_case(era)}_{stem}_{index}"
                spawns.extend(_build(spawner, spawn_id, map_name, ids, lists, flat_ids, report, only))

    return by_file


def _is_spawner(spawner: object) -> bool:
    """A spawner has a location of three whole numbers, entries with a name and whole counts, and a whole count or none."""
    if not isinstance(spawner, dict) or not isinstance(spawner.get("entries"), list):
        return False

    location = spawner.get("location")

    return (
        isinstance(location, list)
        and len(location) >= 3
        and all(is_json_int(part) for part in location[:3])
        and is_json_int(spawner.get("count", 1))
        and all(is_json_int(spawner[key]) for key in ("homeRange",) if key in spawner)
        and _has_bounds(spawner)
        and all(
            isinstance(entry, dict) and isinstance(entry.get("name"), str) and is_json_int(entry.get("maxCount", 0))
            for entry in spawner["entries"]
        )
    )


def _has_bounds(spawner: dict) -> bool:
    """A spawner with spawn bounds has a start and an end with whole x and y, and an end with a whole z."""
    bounds = spawner.get("spawnBounds")

    if not isinstance(bounds, dict):
        return "spawnBounds" not in spawner

    start, end = bounds.get("start"), bounds.get("end")

    return (
        isinstance(start, dict)
        and isinstance(end, dict)
        and all(is_json_int(start.get(key)) for key in ("x", "y"))
        and all(is_json_int(end.get(key)) for key in ("x", "y", "z"))
    )


def _build(
    spawner: dict,
    spawn_id: str,
    folder_map: str,
    ids: set[str],
    lists: set[str],
    flat_ids: dict[str, str],
    report: ConversionReport,
    only: str | None,
) -> list[Spawn]:
    """The regions of one spawner.

    ModernUO picks each spawn among the entries under their maxCount, up to the spawner's count. An entry capped below the count becomes a
    region of its own with its cap; the others share one region with what is left, less the share of the entries no template matches.
    """
    count = max(1, spawner.get("count", 1))
    # By mobile, in entry order: a class listed twice adds its caps.
    capped: list[list] = []
    shared: list[str] = []
    unknown_shared = 0
    capped_total = 0

    for entry in spawner["entries"]:
        name = entry["name"]

        # With "only", the entries of the other classes are left to a run without it.
        if only is not None and (not name.endswith(only) or name in PLACED_BY_UOX3):
            continue

        cap = min(count, entry.get("maxCount", count))
        mobile = mobile_names.resolve(name, flat_ids, ids)

        if mobile is None:
            report.count(f"unknown mobile {name}")

        if cap < count:
            capped_total += cap

            if mobile is not None and cap > 0:
                known = next((pair for pair in capped if pair[0] == mobile), None)

                if known is None:
                    capped.append([mobile, cap])
                else:
                    known[1] = min(count, known[1] + cap)
        elif mobile is None:
            unknown_shared += 1
        elif mobile not in shared:
            shared.append(mobile)

    if not capped and not shared:
        report.count("spawner without known mobiles skipped")

        return []

    rest = max(1, count - capped_total)
    regions: list[Spawn] = []

    if shared:
        # Rounded half away from zero, as .NET does with MidpointRounding.AwayFromZero.
        share = math.floor(rest * len(shared) / (len(shared) + unknown_shared) + 0.5)
        regions.append(_region(spawner, spawn_id, folder_map, shared, max(1, share), lists))

    for mobile, cap in capped:
        regions.append(_region(spawner, f"{spawn_id}_{mobile}", folder_map, [mobile], cap, lists))

    return regions


def _region(spawner: dict, spawn_id: str, folder_map: str, mobiles: list[str], limit: int, lists: set[str]) -> Spawn:
    map_name = map_of(spawner, folder_map)
    low = minutes(spawner, "minDelay")
    spawn = Spawn(
        id=spawn_id,
        map=map_name,
        name=f"{map_name} {', '.join(mobiles)}",
        mobile_ids=[mobile for mobile in mobiles if mobile not in lists],
        npc_list_ids=[mobile for mobile in mobiles if mobile in lists],
        max=limit,
        min_minutes=low,
        max_minutes=max(low, minutes(spawner, "maxDelay")),
    )
    place(spawner, spawn)

    return spawn
