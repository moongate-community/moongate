"""UOX3's NPCs, creatures and name lists as mobile templates and ``names.toml`` (the mobile pass of ``uox``).

Every npc block of ``npc/`` is converted; a ``GET=m_x f_x`` pair becomes one template with a random gender. The result is read back as the
server will read it and every reference is checked.
"""

from __future__ import annotations

import re
import tomllib
from pathlib import Path
from typing import TextIO

from . import dfn, mobiles, names, tomlout, uox_data
from .dfn import DfnBlock, IgnoreCaseDict, IgnoreCaseSet
from .item_index import ItemIndex
from .mobiles import BuildContext, Mobile
from .report import ConversionReport
from .specs import DiceSpec
from .textutil import read_lines, snake_case, write_text

_SCRIPT_ID_RULE = "must be a Lua identifier of lower-case letters, digits and underscores"


class _BadSource(Exception):
    pass


def run(
    mobile_source: Path,
    mobile_destination: Path,
    names_destination: Path,
    items: ItemIndex,
    output: TextIO,
    error: TextIO,
) -> int:
    """Converts the NPCs of UOX3's ``dfndata`` folder (``mobile_source``: npc/, colors/, creatures/ and newbie/) into mobile templates, one file
    per source file under ``mobile_destination``, and the name lists of ``npc/namelists.dfn`` into the file ``names_destination``
    (``names.toml``). ``items`` is what the item pass computed: the ids the NPCs' equipment and loot resolve against. Writes its report to
    ``output``; returns 0 when done, 1 when reading the output back finds an error, 2 for a bad source (nothing written)."""
    try:
        return _run(mobile_source, mobile_destination, names_destination, items, output, error)
    except (_BadSource, OSError, UnicodeError) as exception:
        error.write(f"Mobile conversion failed: {exception}\n")

        return 2


def _run(
    mobile_source: Path,
    mobile_destination: Path,
    names_destination: Path,
    items: ItemIndex,
    output: TextIO,
    error: TextIO,
) -> int:
    # The sources are read before anything is written.
    dictionary = uox_data.load_dictionary(mobile_source / ".." / "dictionaries" / "dictionary.ENG")
    name_lists_path = mobile_source / "npc" / "namelists.dfn"
    name_lists = names.build(dfn.parse(read_lines(name_lists_path)), dictionary) if name_lists_path.is_file() else []
    npc_directory = mobile_source / "npc"
    source_files = (
        sorted((path for path in npc_directory.rglob("*.dfn") if path.is_file() and not _is_skipped_file(npc_directory, path)), key=str)
        if npc_directory.is_dir()
        else []
    )
    report = ConversionReport()
    blocks_by_file: list[tuple[Path, list[DfnBlock]]] = []
    blocks_by_header: IgnoreCaseDict[DfnBlock] = IgnoreCaseDict()

    for file in source_files:
        blocks = dfn.parse(read_lines(file))
        blocks_by_file.append((file, blocks))

        for block in blocks:
            if mobiles.is_special_section(block.header):
                continue

            if block.header in blocks_by_header:
                report.count("duplicate npc header")
            else:
                blocks_by_header[block.header] = block

    creatures = mobile_source / "creatures" / "creatures.dfn"
    movements = mobiles.load_creature_movements(creatures)
    context = BuildContext(
        dictionary,
        items,
        IgnoreCaseSet(blocks_by_header),
        uox_data.load_color_lists(mobile_source / "colors" / "colors.dfn"),
        mobiles.load_creature_sounds(creatures),
        movements,
        _swimming_headers(blocks_by_header, movements),
        report,
    )

    # First every single template, so a pair can resolve both halves wherever they are defined.
    built_by_file = [
        (file, [(block, mobiles.build(block, context)) for block in blocks if blocks_by_header.get(block.header) is block])
        for file, blocks in blocks_by_file
    ]
    by_header: IgnoreCaseDict[Mobile] = IgnoreCaseDict()

    for _, built in built_by_file:
        for block, template in built:
            if template is not None:
                by_header[block.header] = template

    by_id: dict[str, Mobile] = {}

    for template in by_header.values():
        if template.id in by_id:
            raise _BadSource(f"two npc headers become the mobile id '{template.id}'")

        by_id[template.id] = template

    planned: list[tuple[Path, list[Mobile]]] = []

    for file, built in built_by_file:
        templates = [t for block, template in built if (t := template or _merge_pair(block, by_header, by_id, report)) is not None]

        if templates:
            planned.append((file, templates))

    # Everything is built; now it is written.
    write_text(names_destination, names.NAMES_HEADER + names.serialize(name_lists))
    output.write(f"Converted {len(name_lists)} name list(s) to {names_destination}.\n")
    written = 0

    for file, templates in planned:
        relative = file.relative_to(npc_directory)
        target = mobile_destination / relative.with_suffix(".toml")
        write_text(target, mobiles.serialize(templates))
        written += len(templates)
        output.write(f"npc/{relative.as_posix()} -> {target.relative_to(mobile_destination).as_posix()} ({len(templates)} mobile(s))\n")

    output.write(f"Converted {written} mobile(s).\n")
    report.write(output)

    errors, mobile_count, list_count = _verify(mobile_destination, names_destination, items)

    for message in errors:
        error.write(f"Verification failed: {message}\n")

    if errors:
        error.write(f"{len(errors)} verification error(s) found reading the converted mobiles back.\n")

        return 1

    output.write(
        f"Verified {mobile_count} mobile(s) and {list_count} name list(s) read back from disk: no duplicate ids; every base_id, item, loot "
        "and name list resolves; every template validates.\n"
    )

    return 0


def _is_skipped_file(npc_directory: Path, file: Path) -> bool:
    """Name lists are converted on their own; npclists are spawn lists, not npcs."""
    relative = file.relative_to(npc_directory).as_posix().lower()

    return relative == "namelists.dfn" or relative.startswith("npclists/")


def _swimming_headers(blocks_by_header: IgnoreCaseDict[DfnBlock], movements: dict[int, str]) -> IgnoreCaseSet:
    """The headers whose body, set on themselves or the nearest ``GET`` ancestor with an ``ID``, moves in water or both."""
    swimming = IgnoreCaseSet()

    for header in blocks_by_header:
        visited = IgnoreCaseSet()
        current = header

        while current not in visited and current in blocks_by_header:
            visited.add(current)
            block = blocks_by_header[current]
            id_text = block.fields.get("ID")
            body = None if id_text is None else dfn.uox_number(id_text)

            if id_text is not None and body is not None:
                if body in movements:
                    swimming.add(header)

                break

            parents = dfn.parent_targets(block)

            if len(parents) != 1:
                break

            current = parents[0]

    return swimming


def _merge_pair(
    block: DfnBlock, by_header: IgnoreCaseDict[Mobile], by_id: dict[str, Mobile], report: ConversionReport
) -> Mobile | None:
    """``GET=a b``: one template from a male/female pair; any other pair is skipped."""
    targets = dfn.get_targets(block)

    if len(targets) != 2:
        return None

    if targets[0] not in by_header or targets[1] not in by_header:
        report.count("two-target get, unresolved")

        return None

    first, second = by_header[targets[0]], by_header[targets[1]]
    id_ = snake_case(block.header)
    merged = mobiles.try_merge(id_, first, second, lambda template: _resolve(template, by_id), report)

    if merged is not None:
        return merged

    # Not a gender pair ([dragon] GET=graydragon reddragon): UOX3 picks one at random; a template has one base, so the first is kept.
    report.count("two-target get, first target kept")

    return Mobile(id=id_, base_id=first.id)


def _resolve(template: Mobile, by_id: dict[str, Mobile]) -> tuple[str | None, str | None]:
    """Race and gender may come from a base: walk the ``base_id`` chain until each is found."""
    race = gender = None
    seen: set[str] = set()
    current: Mobile | None = template

    while current is not None and current.id not in seen:
        seen.add(current.id)
        race = race if race is not None else current.race
        gender = gender if gender is not None else current.gender
        current = None if current.base_id is None else by_id.get(current.base_id)

    return race, gender


# --- reading the output back ---


def _verify(mobile_destination: Path, names_destination: Path, items: ItemIndex) -> tuple[list[str], int, int]:
    """Reads what was written back, as the server will, and checks every reference and rule."""
    errors: list[str] = []
    list_ids = IgnoreCaseSet(entry.get("id", "") for entry in _tables(names_destination, "names"))
    mobile_tables: list[dict] = []

    if mobile_destination.is_dir():
        for path in sorted(mobile_destination.rglob("*.toml"), key=str):
            mobile_tables += _tables(path, "mobile")

    ids: set[str] = set()

    for table in mobile_tables:
        if table.get("id") in ids:
            errors.append(f"mobile '{table.get('id')}' is defined more than once")

        ids.add(table.get("id"))

    for table in mobile_tables:
        id_ = table.get("id")
        base_id = table.get("base_id")

        if base_id is not None and base_id not in ids:
            errors.append(f"mobile '{id_}' has base_id '{base_id}', which does not exist")

        for entry in table.get("equipment", []):
            errors += [
                f"mobile '{id_}' equips item '{item}', which does not exist"
                for item in entry.get("items", [])
                if item not in items.item_ids
            ]

        errors += [f"mobile '{id_}' has loot '{loot}', which does not exist" for loot in table.get("loot", []) if loot not in items.loot_ids]
        name_list = table.get("name_list")

        if name_list is not None and "{" not in name_list and name_list not in list_ids:
            errors.append(f"mobile '{id_}' has name_list '{name_list}', which does not exist")

        problem = _validate(table)

        if problem is not None:
            errors.append(problem)

    return errors, len(mobile_tables), len(list_ids)


def _tables(path: Path, name: str) -> list[dict]:
    document = tomllib.loads(path.read_bytes().decode("utf-8-sig"))
    found = document.get(name, [])

    return found if isinstance(found, list) else []


def _dice(value: object) -> DiceSpec | None:
    if isinstance(value, bool):
        return None

    return DiceSpec.from_value(value) if isinstance(value, int) else DiceSpec.try_parse(value) if isinstance(value, str) else None


def _validate(table: dict) -> str | None:
    """The rules of ``MobileTemplate.Validate``; the message of the first field out of range, else None."""
    def invalid(field: str, rule: str) -> str:
        return f"Mobile template '{table.get('id')}': {field} {rule}."

    for field in ("strength", "dexterity", "intelligence", "hits", "mana", "stamina", "damage", "armor", "fame", "gold"):
        dice = _dice(table.get(field))

        if dice is not None and dice.min < 0:
            return invalid(field, "must not roll below 0")

    skill_names = {snake_case(skill).replace("_", "") for skill in uox_data.SKILL_TYPES}

    for name, value in (table.get("skills") or {}).items():
        dice = _dice(value)

        if name.replace("_", "").lower() not in skill_names:
            return invalid("skills", f"has '{name}', which is not a skill")

        if dice is not None and (dice.min < 0 or dice.max > 120):
            return invalid("skills", f"'{name}' must roll between 0 and 120")

    for value in (table.get("resistances") or {}).values():
        dice = _dice(value)

        if dice is not None and (dice.min < 0 or dice.max > 100):
            return invalid("resistances", "must roll between 0 and 100")

    if any(isinstance(value, int) and value < 0 for value in (table.get("sounds") or {}).values()):
        return invalid("sounds", "must be 0 or more")

    for entry in table.get("equipment") or []:
        items = entry.get("items", [])

        if not items or any(not isinstance(item, str) or not item.strip() for item in items):
            return invalid("equipment", "must name at least one item and no empty item id")

    if any(not key.strip() for key in (table.get("tags") or {})):
        return invalid("tags", "must not have an empty key")

    blood_hue = table.get("blood_hue")

    if isinstance(blood_hue, int) and not -1 <= blood_hue <= 65535:
        return invalid("blood_hue", "must be from -1 to 65535")

    flee_at = table.get("flee_at")

    if isinstance(flee_at, int) and not -1 <= flee_at <= 100:
        return invalid("flee_at", "must be from -1 to 100")

    script_id = table.get("script_id")

    if script_id is not None and not re.fullmatch(r"[a-z_][a-z0-9_]*", script_id):
        return invalid("script_id", _SCRIPT_ID_RULE)

    return None
