"""UOX3's NPC lists and spawn regions as ``templates/npc_lists`` and ``templates/spawns`` (the spawn pass of ``uox``).

Run after the mobile pass, against the mobile templates it wrote: a list or a spawn names only mobiles and lists that exist. Everything
written is read back and checked the way the server's loaders check it.
"""

from __future__ import annotations

import os
import re
import tomllib
from dataclasses import dataclass
from pathlib import Path
from typing import TextIO

from . import dfn
from .dfn import DfnBlock, IgnoreCaseDict
from .report import ConversionReport
from .spawn import MAP_NAMES, Spawn, SpawnArea, map_key
from .spawn import serialize as serialize_spawns
from .textutil import read_lines, snake_case, trim, try_int, write_text
from .tomlout import basic

_LIST_HEADER_PREFIX = "npclist "
_NESTED_LIST_PREFIX = "npclist="
_SPAWN_HEADER_PREFIX = "regionspawn "

# The era of the shard: the client is a modern one. UOX3's own default, lbr, keeps the same regions.
_SHARD_ERA = "tol"

# What a region never takes from the region it GETs, as UOX3 loads a parent without them.
_NOT_INHERITED = {"WORLD", "NPC", "NPCLIST", "ERAS"}

# UOX3's WORLD numbers.
_WORLDS = {0: "Felucca", 1: "Trammel", 2: "Ilshenar", 3: "Malas", 4: "Tokuno"}

_SPAWN_FILE_PREFIX = re.compile(r"^spawn_[a-z]+_", re.IGNORECASE | re.ASCII)


@dataclass
class _ListEntry:
    weight: int = 1
    mobile_id: str | None = None
    npc_list_id: str | None = None


@dataclass
class _NpcList:
    id: str
    entries: list[_ListEntry]


def run(
    mobile_source: Path,
    mobile_destination: Path,
    npc_lists_destination: Path,
    spawns_destination: Path,
    output: TextIO,
    error: TextIO,
) -> int:
    """Converts the ``[NPCLIST ...]`` blocks of ``npc/`` of UOX3's ``dfndata`` folder (``mobile_source``) into npc lists under
    ``npc_lists_destination``, and the ``[REGIONSPAWN ...]`` blocks of ``spawn/`` into spawn regions under ``spawns_destination``, one folder
    per map. ``mobile_destination`` is where the mobile pass wrote the mobile templates, which the lists and spawns name. Writes its report to
    ``output``; returns 0 when done, 1 when reading what was written back finds a name that does not resolve or a spawn the loader refuses."""
    report = ConversionReport()
    mobile_ids = read_mobile_ids(mobile_destination)
    list_ids = _convert_lists(mobile_source, npc_lists_destination, mobile_ids, report, output)
    spawns = _convert_spawns(mobile_source, spawns_destination, mobile_ids, list_ids, report, output)

    report.write(output)
    output.write(f"Converted {len(list_ids)} npc list(s) and {spawns} spawn region(s).\n")

    errors, verified_lists, verified_spawns = _verify(npc_lists_destination, spawns_destination, mobile_ids)

    if errors:
        for message in errors:
            error.write(f"Verification failed: {message}\n")

        error.write(f"{len(errors)} verification error(s) found reading the converted spawns back.\n")

        return 1

    output.write(
        f"Verified {verified_lists} npc list(s) and {verified_spawns} spawn region(s) read back from disk: every mobile and "
        "list resolves, every spawn has an area and its times in order.\n"
    )

    return 0


def read_mobile_ids(mobile_destination: Path) -> set[str]:
    """The ids of the ``[[mobile]]`` entries of every toml file under the mobile templates folder; none when it does not exist."""
    ids: set[str] = set()

    if not mobile_destination.is_dir():
        return ids

    for file in sorted(mobile_destination.rglob("*.toml")):
        for mobile in _tables(tomllib.loads(file.read_text(encoding="utf-8-sig")), "mobile"):
            if isinstance(mobile.get("id"), str):
                ids.add(mobile["id"])

    return ids


def _tables(document: dict, name: str) -> list[dict]:
    value = document.get(name, [])

    return [entry for entry in value if isinstance(entry, dict)] if isinstance(value, list) else []


def _dfn_files(directory: Path) -> list[Path]:
    if not directory.is_dir():
        return []

    return sorted((path for path in directory.rglob("*.dfn") if path.is_file()), key=str)


# --- the npc lists ---


def _convert_lists(mobile_source: Path, destination: Path, mobile_ids: set[str], report: ConversionReport, output: TextIO) -> set[str]:
    npc_directory = mobile_source / "npc"
    by_file: list[tuple[str, list[_NpcList]]] = []
    by_id: dict[str, _NpcList] = {}
    raw: dict[str, list[tuple[_ListEntry, bool]]] = {}

    for file in _dfn_files(npc_directory):
        lists: list[_NpcList] = []

        for block in dfn.parse(read_lines(file)):
            if not block.header.lower().startswith(_LIST_HEADER_PREFIX):
                continue

            npc_list = _NpcList(snake_case(trim(block.header[len(_LIST_HEADER_PREFIX) :])), [])

            if npc_list.id in by_id:
                report.count("duplicate npc list")

                continue

            by_id[npc_list.id] = npc_list
            raw[npc_list.id] = [parsed for line in block.entries if (parsed := _parse_list_entry(line)) is not None]
            lists.append(npc_list)

        if lists:
            root = str(npc_directory / "npclists")
            relative = os.path.relpath(file, root) if str(file).startswith(root) else os.path.relpath(file, npc_directory)
            by_file.append((relative, lists))

    # An unweighted NPCLIST=x brings x's entries into the list, as UOX3 splices it; a weighted one stays one pick.
    for npc_list in by_id.values():
        npc_list.entries = _splice(npc_list.id, raw, set())

    # Drop what does not resolve, then the lists left empty and the entries naming them, until nothing changes.
    for npc_list in by_id.values():
        kept: list[_ListEntry] = []

        for entry in npc_list.entries:
            unresolved = entry.npc_list_id not in by_id if entry.mobile_id is None else entry.mobile_id not in mobile_ids

            if unresolved:
                report.count("unresolved npc list entry")
            else:
                kept.append(entry)

        npc_list.entries = kept

    changed = True

    while changed:
        changed = False

        for empty in [npc_list for npc_list in by_id.values() if not npc_list.entries]:
            del by_id[empty.id]
            report.count("empty npc list")
            changed = True

        for npc_list in by_id.values():
            kept = [entry for entry in npc_list.entries if entry.npc_list_id is None or entry.npc_list_id in by_id]

            if len(kept) != len(npc_list.entries):
                npc_list.entries = kept
                changed = True

    for relative, lists in by_file:
        survivors = [npc_list for npc_list in lists if npc_list.id in by_id]

        if not survivors:
            continue

        output_path = Path(os.path.join(destination, os.path.splitext(relative)[0] + ".toml"))
        write_text(output_path, _serialize_lists(survivors))
        output.write(f"npc/{Path(relative).as_posix()} -> {Path(os.path.relpath(output_path, destination)).as_posix()} ({len(survivors)} npc list(s))\n")

    return set(by_id)


def _splice(list_id: str, raw: dict[str, list[tuple[_ListEntry, bool]]], visiting: set[str]) -> list[_ListEntry]:
    entries: list[_ListEntry] = []

    if list_id not in raw or list_id in visiting:
        return entries

    visiting.add(list_id)

    for entry, splice in raw[list_id]:
        if splice:
            # A spliced list that does not exist is kept as a reference, so it is reported as unresolved.
            assert entry.npc_list_id is not None
            entries.extend(_splice(entry.npc_list_id, raw, visiting) if entry.npc_list_id in raw else [entry])
        else:
            entries.append(entry)

    visiting.discard(list_id)

    return entries


def _parse_list_entry(raw_line: str) -> tuple[_ListEntry, bool] | None:
    """``20|gorilla``, ``orc``, ``NPCLIST=trolls`` (spliced) or ``7|NPCLIST=allophidians`` (one pick)."""
    line = trim(raw_line)
    weight = 1
    pipe = line.find("|")

    if pipe >= 0:
        parsed = try_int(trim(line[:pipe]))
        weight = parsed if parsed is not None and parsed > 0 else 1
        line = trim(line[pipe + 1 :])

    if not line:
        return None

    if line.lower().startswith(_NESTED_LIST_PREFIX):
        return _ListEntry(weight, npc_list_id=snake_case(trim(line[len(_NESTED_LIST_PREFIX) :]))), pipe < 0

    return _ListEntry(weight, mobile_id=snake_case(line)), False


def _serialize_lists(lists: list[_NpcList]) -> str:
    """The lists as the ``[[npc_list]]`` tables Tomlyn writes."""
    blocks = []

    for npc_list in lists:
        entries = []

        for entry in npc_list.entries:
            lines = ["[[npc_list.entries]]", f"weight = {entry.weight}"]

            if entry.mobile_id is not None:
                lines.append(f"mobile_id = {basic(entry.mobile_id)}")

            if entry.npc_list_id is not None:
                lines.append(f"npc_list_id = {basic(entry.npc_list_id)}")

            entries.append("\n".join(lines) + "\n")

        blocks.append(f"[[npc_list]]\nid = {basic(npc_list.id)}\n" + "\n".join(entries))

    return "\n".join(blocks)


# --- the spawn regions ---


def _convert_spawns(
    mobile_source: Path, destination: Path, mobile_ids: set[str], list_ids: set[str], report: ConversionReport, output: TextIO
) -> int:
    blocks_by_file = [(file, dfn.parse(read_lines(file))) for file in _dfn_files(mobile_source / "spawn")]
    by_number: IgnoreCaseDict[DfnBlock] = IgnoreCaseDict()

    for _, blocks in blocks_by_file:
        # UOX3 keeps the last definition of a header.
        for block in blocks:
            if block.header.lower().startswith(_SPAWN_HEADER_PREFIX):
                by_number[trim(block.header[len(_SPAWN_HEADER_PREFIX) :])] = block

    # Collected first: files of two folders can hold regions of the same map under the same name.
    by_output: dict[tuple[str, str], list[Spawn]] = {}

    for file, blocks in blocks_by_file:
        folder = file.parent.name
        name = snake_case(_SPAWN_FILE_PREFIX.sub("", file.stem))

        for block in blocks:
            if not block.header.lower().startswith(_SPAWN_HEADER_PREFIX):
                continue

            number = trim(block.header[len(_SPAWN_HEADER_PREFIX) :])

            if by_number[number] is not block:
                report.count("duplicate spawn region")

                continue

            spawn = _build_spawn(number, _resolve(block, by_number, set()), folder, mobile_ids, list_ids, report)

            if spawn is not None:
                by_output.setdefault((spawn.map, name), []).append(spawn)

    written = 0

    for (map_name, name), spawns in sorted(by_output.items(), key=lambda item: (MAP_NAMES.index(item[0][0]), item[0][1])):
        output_path = destination / map_key(map_name) / f"{name}.toml"
        write_text(output_path, serialize_spawns(spawns))
        written += len(spawns)
        output.write(f"spawns/{output_path.relative_to(destination).as_posix()} ({len(spawns)} spawn region(s))\n")

    return written


def _resolve(block: DfnBlock, by_number: IgnoreCaseDict[DfnBlock], seen: set[str]) -> list[tuple[str, str]]:
    """The ``KEY=VALUE`` lines of the region, after those of the region it GETs; its own keys win, and naming an NPC or a list replaces what it
    would inherit to spawn."""
    own = [field for line in block.entries if (field := _split_field(line)) is not None]
    parent = next((value for key, value in own if key == "GET"), None)
    without_get = [field for field in own if field[0] != "GET"]

    if parent is None or parent in seen:
        return without_get

    seen.add(parent)

    if parent not in by_number:
        return without_get

    inherited = _resolve(by_number[parent], by_number, seen)
    own_keys = {key for key, _ in own}

    return [field for field in inherited if field[0] not in own_keys and field[0] not in _NOT_INHERITED] + without_get


def _split_field(line: str) -> tuple[str, str] | None:
    equals = line.find("=")

    return (trim(line[:equals]).upper(), trim(line[equals + 1 :])) if equals > 0 else None


def _build_spawn(
    number: str, fields: list[tuple[str, str]], folder: str, mobile_ids: set[str], list_ids: set[str], report: ConversionReport
) -> Spawn | None:
    def get(key: str) -> str | None:
        return next((value for field_key, value in reversed(fields) if field_key == key), None)

    def numeric(*keys: str) -> int | None:
        for key in keys:
            text = get(key)

            if text is not None and (value := dfn.uox_number(text)) is not None:
                return value

        return None

    mobiles = [snake_case(value) for key, value in fields if key == "NPC"]
    lists = [snake_case(value) for key, value in fields if key == "NPCLIST"]

    if not mobiles and not lists:
        report.count("spawn region(s) without NPCs skipped")

        return None

    eras = get("ERAS")

    if eras is not None and not any(trim(era).lower() == _SHARD_ERA for era in eras.split(",")):
        report.count("spawn region of another era skipped")

        return None

    mobiles = [item for item in mobiles if item in mobile_ids]
    lists = [item for item in lists if item in list_ids]

    if not mobiles and not lists:
        report.count("unresolved spawn")

        return None

    world = numeric("WORLD")
    map_name = _WORLDS[world] if world in _WORLDS else _map_of_folder(folder)
    low, high = numeric("MINTIME") or 0, numeric("MAXTIME") or 0
    spawn = Spawn(
        id=f"{map_key(map_name)}_{number}",
        map=map_name,
        name=get("NAME"),
        mobile_ids=list(dict.fromkeys(mobiles)),
        npc_list_ids=list(dict.fromkeys(lists)),
        max=numeric("MAXNPCS", "MAXNPC") or 0,
        # UOX3 picks between the two whatever their order.
        min_minutes=min(low, high),
        max_minutes=max(low, high),
        call=1 if (call := numeric("CALL")) is None else call,
        pref_z=numeric("PREFZ"),
        z=numeric("DEFZ"),
        only_outside=numeric("ONLYOUTSIDE") == 1,
    )
    x1, y1, x2, y2 = numeric("X1"), numeric("Y1"), numeric("X2"), numeric("Y2")

    if x1 is not None and y1 is not None and x2 is not None and y2 is not None:
        spawn.areas.append(SpawnArea(min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)))

    for key, value in fields:
        if key != "EXCLUDEAREA":
            continue

        parts = value.split(",")
        numbers = [dfn.uox_number(trim(part)) for part in parts]

        if len(parts) == 4 and all(number is not None for number in numbers):
            a, b, c, d = (number or 0 for number in numbers)
            spawn.exclude.append(SpawnArea(min(a, c), min(b, d), max(a, c), max(b, d)))

    if spawn.max < 1 or not spawn.areas:
        report.count("spawn region(s) without a maximum or an area skipped")

        return None

    return spawn


def _map_of_folder(folder: str) -> str:
    return {"trammel": "Trammel", "ilishenar": "Ilshenar", "ilshenar": "Ilshenar", "malas": "Malas", "tokuno": "Tokuno"}.get(
        folder.lower(), "Felucca"
    )


# --- reading back ---


def _verify(npc_lists_destination: Path, spawns_destination: Path, mobile_ids: set[str]) -> tuple[list[str], int, int]:
    """Reads back what was written, as the server will, and checks what its loaders check."""
    errors: list[str] = []
    lists: list[dict] = []

    if npc_lists_destination.is_dir():
        for file in sorted(npc_lists_destination.rglob("*.toml")):
            lists.extend(_tables(tomllib.loads(file.read_text(encoding="utf-8-sig")), "npc_list"))

    list_ids = {str(npc_list.get("id", "")) for npc_list in lists}

    for npc_list in lists:
        for entry in _tables(npc_list, "entries"):
            mobile = entry.get("mobile_id")
            unresolved = mobile not in mobile_ids if mobile is not None else entry.get("npc_list_id", "") not in list_ids

            if unresolved:
                errors.append(f"npc list '{npc_list.get('id', '')}' names '{mobile if mobile is not None else entry.get('npc_list_id')}', which does not resolve.")

    spawns: list[dict] = []

    if spawns_destination.is_dir():
        for file in sorted(spawns_destination.rglob("*.toml")):
            spawns.extend(_tables(tomllib.loads(file.read_text(encoding="utf-8-sig")), "spawn"))

    for spawn in spawns:
        spawn_id = spawn.get("id", "")

        if any(item not in mobile_ids for item in spawn.get("mobile_ids", [])) or any(item not in list_ids for item in spawn.get("npc_list_ids", [])):
            errors.append(f"spawn '{spawn_id}' names a mobile or a list that does not resolve.")

        areas = _tables(spawn, "areas")
        excluded = _tables(spawn, "exclude")

        if (
            spawn.get("max", 1) < 1
            or spawn.get("call", 1) < 1
            or spawn.get("min_minutes", 0) > spawn.get("max_minutes", 0)
            or not areas
            or any(area.get("x1", 0) > area.get("x2", 0) or area.get("y1", 0) > area.get("y2", 0) for area in areas + excluded)
        ):
            errors.append(f"spawn '{spawn_id}' has a max, call, times or area the loader refuses.")

    seen: dict[str, int] = {}

    for spawn in spawns:
        seen[spawn.get("id", "")] = seen.get(spawn.get("id", ""), 0) + 1

    errors.extend(f"spawn '{spawn_id}' is written twice." for spawn_id, count in seen.items() if count > 1)

    return errors, len(lists), len(spawns)
