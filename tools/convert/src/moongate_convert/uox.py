"""``uox``: UOX3's ``.dfn`` item blocks and loot lists as item and loot templates, and with ``--mobile-source`` its NPCs, name lists, starting
items, NPC lists and spawn regions.

Reads every block of every source file first, flattens the ``get=`` targets that have no ``id=``, computes the id of every block from the block
alone, then writes one item file per source file and one loot file per loot table, reads what it wrote back and checks that no id is defined
twice and that every base id and loot reference resolves. Exit codes: 0 done, 1 the read back found an error, 2 a bad source or options.
"""

from __future__ import annotations

import os
import tomllib
from pathlib import Path
from typing import TextIO

from . import dfn, items, uox_data, uox_mobiles, uox_spawns, uox_starting_items
from .dfn import DfnBlock, IgnoreCaseDict, IgnoreCaseSet
from .item_index import ItemIndex
from .textutil import read_lines, snake_case, walk_files, write_text


def run(
    source: Path,
    destination: Path,
    loot_destination: Path | None,
    output: TextIO,
    error: TextIO,
    mobile_source: Path | None = None,
    mobile_destination: Path | None = None,
    names_destination: Path | None = None,
    starting_items_destination: Path | None = None,
    scripts_source: Path | None = None,
    npc_lists_destination: Path | None = None,
    spawns_destination: Path | None = None,
) -> int:
    invalid = _validate_options(mobile_source, mobile_destination, names_destination, starting_items_destination, error)

    if invalid is not None:
        return invalid

    if (npc_lists_destination is None) != (spawns_destination is None):
        error.write("--npc-lists-destination and --spawns-destination go together: a spawn names npc lists.\n")

        return 2

    if npc_lists_destination is not None and mobile_source is None:
        error.write("--npc-lists-destination and --spawns-destination need --mobile-source, which holds npc/ and spawn/.\n")

        return 2

    source = _full(source)
    destination = _full(destination)
    loot_destination = _full(loot_destination) if loot_destination is not None else None

    if not source.exists():
        error.write(f"Source does not exist: {source}\n")

        return 2

    scripts: uox_data.ScriptAssociations | None = None

    if scripts_source is not None:
        try:
            scripts = uox_data.ScriptAssociations.load(_full(scripts_source))
        except OSError as exception:
            error.write(f"{exception}\n")

            return 2

    try:
        source_files = [source] if source.is_file() else walk_files(source, ".dfn")
    except OSError as exception:
        error.write(f"{exception}\n")

        return 2

    if not source_files:
        error.write(f"No .dfn files found under {source}\n")

        return 2

    try:
        return _convert(
            source,
            destination,
            loot_destination,
            source_files,
            scripts,
            output,
            error,
            mobile_source,
            mobile_destination,
            names_destination,
            starting_items_destination,
            npc_lists_destination,
            spawns_destination,
        )
    except (ValueError, OSError, tomllib.TOMLDecodeError) as exception:
        error.write(f"UOX3 conversion failed: {exception}\n")

        return 2


def _full(path: Path) -> Path:
    return Path(os.path.abspath(path))


def _convert(
    source: Path,
    destination: Path,
    loot_destination: Path | None,
    source_files: list[Path],
    scripts: uox_data.ScriptAssociations | None,
    output: TextIO,
    error: TextIO,
    mobile_source: Path | None,
    mobile_destination: Path | None,
    names_destination: Path | None,
    starting_items_destination: Path | None,
    npc_lists_destination: Path | None,
    spawns_destination: Path | None,
) -> int:
    blocks_by_file, blocks_by_header = _read_blocks(source_files, error)
    flat_by_header = _flatten_blocks(blocks_by_file, blocks_by_header)

    convert_loot = loot_destination is not None
    id_by_header: IgnoreCaseDict[str] = IgnoreCaseDict()
    item_name_by_id: dict[str, str] = {}
    loot_id_by_header: IgnoreCaseDict[str] = IgnoreCaseDict()
    known_loot_ids = IgnoreCaseSet()
    _compute_ids(blocks_by_header, convert_loot, id_by_header, item_name_by_id, loot_id_by_header, known_loot_ids)

    _write_items_and_loot(
        source,
        destination,
        loot_destination,
        blocks_by_file,
        flat_by_header,
        id_by_header,
        item_name_by_id,
        loot_id_by_header,
        known_loot_ids,
        output,
        scripts,
    )

    # A real read back of what was written to disk, not a re-check of the resolution that ran in memory: it also catches a TOML round trip going
    # wrong, and a pair of ids like "Base-Item"/"base_item" that collide only after snake case.
    errors, item_count, loot_count = _verify_output(destination, loot_destination)

    if errors:
        for message in errors:
            error.write(f"Verification failed: {message}\n")

        error.write(f"{len(errors)} verification error(s) found reading the converted output back.\n")

        return 1

    output.write(
        f"Verified {item_count} item(s) and {loot_count} loot table(s) read back from disk: "
        "no duplicate ids, every BaseId and loot reference resolves.\n"
    )

    if mobile_source is None:
        return 0

    index = ItemIndex(id_by_header, blocks_by_header, known_loot_ids)
    mobile_source = _full(mobile_source)

    # The destinations were validated as given for this mode.
    assert mobile_destination is not None and names_destination is not None
    result = uox_mobiles.run(mobile_source, _full(mobile_destination), _full(names_destination), index, output, error)

    if result != 0:
        return result

    if starting_items_destination is not None:
        result = uox_starting_items.run(mobile_source, _full(starting_items_destination), index, output, error)

        if result != 0:
            return result

    if npc_lists_destination is None:
        return 0

    assert spawns_destination is not None

    return uox_spawns.run(
        mobile_source, _full(mobile_destination), _full(npc_lists_destination), _full(spawns_destination), output, error
    )


def _validate_options(
    mobile_source: Path | None,
    mobile_destination: Path | None,
    names_destination: Path | None,
    starting_items_destination: Path | None,
    error: TextIO,
) -> int | None:
    """The mobile options go together, and starting items need the mobile source that holds newbie/newbie.dfn."""
    if (mobile_source is None) != (mobile_destination is None) or (mobile_source is None) != (names_destination is None):
        error.write("--mobile-source, --mobile-destination and --names-destination go together: give all three or none.\n")

        return 2

    if starting_items_destination is not None and mobile_source is None:
        error.write("--starting-items-destination needs --mobile-source, which holds newbie/newbie.dfn.\n")

        return 2

    return None


def _read_blocks(
    source_files: list[Path], error: TextIO
) -> tuple[dict[Path, list[DfnBlock]], IgnoreCaseDict[DfnBlock]]:
    """Parses every file. UOX3 keeps the last definition of a header (scriptc.cpp overwrites defEntries[section]), and the files come in
    order so the result does not depend on the filesystem."""
    blocks_by_file: dict[Path, list[DfnBlock]] = {}
    blocks_by_header: IgnoreCaseDict[DfnBlock] = IgnoreCaseDict()

    for file in source_files:
        blocks = dfn.parse(read_lines(file))
        blocks_by_file[file] = blocks

        for block in blocks:
            uox_data.apply_fixes(block)

            if block.header in blocks_by_header:
                error.write(f"Duplicate block '[{block.header}]' in {file}; keeping this later one, as UOX3 does.\n")

            blocks_by_header[block.header] = block

    return blocks_by_file, blocks_by_header


def _flatten_blocks(
    blocks_by_file: dict[Path, list[DfnBlock]], blocks_by_header: IgnoreCaseDict[DfnBlock]
) -> IgnoreCaseDict[DfnBlock]:
    """Inlined for the fields only: ids come from each block's own lines, so an inherited name= never renames a bare-hex block. Returns the
    flattened block kept for each header."""
    flat_by_header: IgnoreCaseDict[DfnBlock] = IgnoreCaseDict()

    for blocks in blocks_by_file.values():
        for index, block in enumerate(blocks):
            is_kept = blocks_by_header[block.header] is block
            blocks[index] = items.flatten(block, blocks_by_header)

            if is_kept:
                flat_by_header[blocks[index].header] = blocks[index]

    return flat_by_header


def _compute_ids(
    blocks_by_header: IgnoreCaseDict[DfnBlock],
    convert_loot: bool,
    id_by_header: IgnoreCaseDict[str],
    item_name_by_id: dict[str, str],
    loot_id_by_header: IgnoreCaseDict[str],
    known_loot_ids: IgnoreCaseSet,
) -> None:
    """Every block's id is computed once, up front, from the block alone, so a get= chain or a loot entry resolves the same way whatever order
    the files scan in. A block with no id= of its own but one parent that converted (UOX3's magic items, journals and other variants: get=0x0df1
    plus a name and a colour) is a template too, id = its header; repeated until nothing changes, so a variant of a variant converts once its
    parent has."""

    def add_item(block: DfnBlock, item_id: str) -> None:
        id_by_header[block.header] = item_id

        # Carried only so a loot entry can leave a readable comment: "0x19b7" alone says nothing.
        name = block.fields.get("name")

        if name:
            item_name_by_id[item_id] = name

    for block in blocks_by_header.values():
        loot_id = items.try_get_loot_id(block.header) if convert_loot else None

        if loot_id is not None:
            loot_id_by_header[block.header] = loot_id
            known_loot_ids.add(loot_id)
        elif (computed := items.try_compute_id(block)) is not None:
            add_item(block, computed[0])

    added = True

    while added:
        added = False

        for block in blocks_by_header.values():
            if block.header in id_by_header or block.header in loot_id_by_header:
                continue

            parent = items.single_parent(block)

            if parent is None or parent not in id_by_header:
                continue

            add_item(block, snake_case(block.header))
            added = True


def _write_items_and_loot(
    source: Path,
    destination: Path,
    loot_destination: Path | None,
    blocks_by_file: dict[Path, list[DfnBlock]],
    flat_by_header: IgnoreCaseDict[DfnBlock],
    id_by_header: IgnoreCaseDict[str],
    item_name_by_id: dict[str, str],
    loot_id_by_header: IgnoreCaseDict[str],
    known_loot_ids: IgnoreCaseSet,
    output: TextIO,
    scripts: uox_data.ScriptAssociations | None,
) -> None:
    """Writes one item file per source file and one loot file per table, then the summary of what was skipped."""
    written = 0
    loot_written = 0
    skipped_duplicate = 0
    skipped_no_id = 0
    skipped_unresolved_loot_entry = 0

    if loot_destination is not None:
        loot_destination.mkdir(parents=True, exist_ok=True)

    root = source if source.is_dir() else source.parent

    for file, blocks in blocks_by_file.items():
        templates: list[items.ItemTemplate] = []
        loot_written_for_file = 0

        for block in blocks:
            # A header seen again later in the scan lost the "Duplicate block" warning; it must also lose the conversion, or the same id
            # comes out of two files (real UOX3 data does this: food/rawfoods.dfn and misc/rawfoods.dfn both define [0x1e15]).
            if flat_by_header[block.header] is not block:
                skipped_duplicate += 1

                continue

            loot_id = loot_id_by_header.get(block.header) if loot_destination is not None else None

            if loot_id is not None and loot_destination is not None:
                loot, skipped = items.build_loot(block, loot_id, id_by_header, item_name_by_id, known_loot_ids)
                skipped_unresolved_loot_entry += skipped

                # Each loot table gets its own file, named after its own id: reviewing or hand-editing one has no reason to load every other
                # table defined in the same source .dfn alongside it.
                write_text(loot_destination / f"{loot.id}.toml", items.serialize_loot(loot))
                loot_written += 1
                loot_written_for_file += 1

                continue

            template = items.build_item(block, id_by_header, scripts)

            if template is None:
                skipped_no_id += 1

                continue

            templates.append(template)

        relative = file.relative_to(root)

        if templates:
            output_path = destination / relative.with_suffix(".toml")
            write_text(output_path, items.serialize_items(templates))
            written += len(templates)
            output.write(f"{relative.as_posix()} -> {output_path.relative_to(destination).as_posix()} ({len(templates)} item(s))\n")

        if loot_written_for_file > 0:
            output.write(f"{relative.as_posix()} -> {loot_written_for_file} loot table(s), one file each under --loot-destination\n")

    output.write(
        f"Converted {written} item(s) and {loot_written} loot table(s); skipped {skipped_no_id} block(s) with no "
        f"id= of their own, {skipped_duplicate} duplicate of an already-converted header, and "
        f"{skipped_unresolved_loot_entry} loot entry/entries pointing at nothing this converter could resolve.\n"
    )


def _verify_output(destination: Path, loot_destination: Path | None) -> tuple[list[str], int, int]:
    """Reads every .toml back from the destinations and checks that no two items or loot tables share an id and that every base id and loot
    reference names something that was written."""
    errors: list[str] = []
    templates = _read_all(destination, "item")
    item_ids: set[str] = set()

    for item in templates:
        if item.get("id", "") in item_ids:
            errors.append(f"item '{item.get('id', '')}' is defined more than once")

        item_ids.add(item.get("id", ""))

    for item in templates:
        base_id = item.get("base_id")

        if base_id is not None and base_id not in item_ids:
            errors.append(f"item '{item.get('id', '')}' has BaseId '{base_id}', which does not exist")

    if loot_destination is None:
        return errors, len(templates), 0

    tables = _read_all(loot_destination, "loot")
    loot_ids: set[str] = set()

    for table in tables:
        if table.get("id", "") in loot_ids:
            errors.append(f"loot table '{table.get('id', '')}' is defined more than once")

        loot_ids.add(table.get("id", ""))

    for table in tables:
        for entry in table.get("entries", []):
            item_id = entry.get("item_id")
            nested_id = entry.get("loot_template_id")

            if item_id is not None and item_id not in item_ids:
                errors.append(f"loot table '{table.get('id', '')}' has an entry with ItemId '{item_id}', which does not exist")

            if nested_id is not None and nested_id not in loot_ids:
                errors.append(f"loot table '{table.get('id', '')}' has an entry with LootTemplateId '{nested_id}', which does not exist")

    return errors, len(templates), len(tables)


def _read_all(root: Path, table: str) -> list[dict]:
    """The ``[[<table>]]`` entries of every .toml under a folder; none when the folder was never written to (a source with no items at all, or
    --loot-destination on a run with no LOOTLIST blocks)."""
    entities: list[dict] = []

    if not root.is_dir():
        return entities

    for path in walk_files(root, ".toml"):
        found = tomllib.loads(path.read_text(encoding="utf-8-sig")).get(table, [])

        # A file in the destination this run did not write can have any shape; an entry that is no table is not one.
        if isinstance(found, list):
            entities.extend(entry for entry in found if isinstance(entry, dict))

    return entities
