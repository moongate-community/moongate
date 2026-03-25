#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Dict, Iterable, List, Sequence

try:
    from scripts.modernuo_loot_tooling import (
        build_loot_id,
        ensure_loot_support_items,
        build_output_paths,
        extract_balanced_block,
        load_item_template_index,
        normalize_identifier,
        parse_dice_range,
        resolve_item_reference,
        split_top_level_arguments,
        strip_comments,
        write_json_array,
    )
except ModuleNotFoundError:  # pragma: no cover - direct CLI execution
    from modernuo_loot_tooling import (
        build_loot_id,
        ensure_loot_support_items,
        build_output_paths,
        extract_balanced_block,
        load_item_template_index,
        normalize_identifier,
        parse_dice_range,
        resolve_item_reference,
        split_top_level_arguments,
        strip_comments,
        write_json_array,
    )

LOOTPACK_ARRAY_PATTERN = re.compile(r"public static readonly LootPackItem\[\]\s+(\w+)\s*=\s*\[", re.MULTILINE)
LOOTPACK_PATTERN = re.compile(r"public static readonly LootPack\s+(\w+)\s*=\s*new\(", re.MULTILINE)
LOOTPACK_ALIAS_PATTERN = re.compile(r"public static LootPack\s+(\w+)\s*=>\s*(.+?);", re.MULTILINE | re.DOTALL)
TYPE_ARRAY_PATTERN = re.compile(r"public static Type\[\]\s+(\w+)\s*\{\s*get;\s*\}\s*=\s*\{", re.MULTILINE)
CREATURE_CLASS_PATTERN = re.compile(r"public partial class\s+(\w+)\s*:\s*BaseCreature", re.MULTILINE)
GENERATE_LOOT_PATTERN = re.compile(r"public override void GenerateLoot\(\)\s*\{", re.MULTILINE)
FILLABLE_CONTENT_PATTERN = re.compile(
    r"private static readonly FillableContent\s+(\w+)\s*=\s*new\(",
    re.MULTILINE,
)
PACK_ITEM_CONSTRUCTOR_PATTERN = re.compile(r"PackItem\s*\(\s*new\s+(\w+)\s*\(\s*(\d+)?\s*\)\s*\);", re.MULTILINE)
PACK_ITEM_SEED_FACTORY_PATTERN = re.compile(
    r"PackItem\s*\(\s*Seed\.(?:RandomPeculiarSeed|RandomBonsaiSeed)\s*\([^)]*\)\s*\);",
    re.MULTILINE,
)
PACK_ITEM_RANDOM_REAGENT_PATTERN = re.compile(
    r"PackItem\s*\(\s*Loot\.Random(?:Possible|Necromancy)?Reagent\s*\(\s*\)\s*\);",
    re.MULTILINE,
)


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Generate Moongate loot templates from ModernUO sources.")
    parser.add_argument("modernuo_root", type=Path, help="Path to the ModernUO repository root")
    parser.add_argument("repo_root", type=Path, nargs="?", default=Path("."), help="Path to the Moongate repo root")
    return parser


def build_report() -> Dict[str, object]:
    return {"generated": [], "skipped": [], "missingItems": [], "unmappedPatterns": []}


def append_warning(report: Dict[str, object], bucket: str, value: str) -> None:
    entries = report[bucket]
    if value not in entries:
        entries.append(value)


def try_resolve_reference(
    type_name: str,
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
    context: str,
) -> Dict[str, object] | None:
    resolved = resolve_item_reference(type_name, item_index, tag_index)
    if resolved is None:
        append_warning(report, "missingItems", f"{context}: {type_name}")
    return resolved


def parse_loot_pack_arrays(text: str) -> Dict[str, List[Dict[str, object]]]:
    text = strip_comments(text)
    arrays: Dict[str, List[Dict[str, object]]] = {}

    for match in LOOTPACK_ARRAY_PATTERN.finditer(text):
        name = match.group(1)
        body, _ = extract_balanced_block(text, match.end() - 1, "[", "]")
        items = []
        for item_match in re.finditer(r"new LootPackItem\(typeof\((\w+)\),\s*(\d+)\)", body):
            items.append({"typeName": item_match.group(1), "weight": int(item_match.group(2))})

        arrays[name] = items

    return arrays


def parse_type_arrays(text: str) -> Dict[str, List[str]]:
    text = strip_comments(text)
    arrays: Dict[str, List[str]] = {}

    for match in TYPE_ARRAY_PATTERN.finditer(text):
        name = match.group(1)
        body, _ = extract_balanced_block(text, match.end() - 1, "{", "}")
        arrays[name] = re.findall(r"typeof\((\w+)\)", body)

    return arrays


def parse_loot_packs(text: str) -> Dict[str, List[Dict[str, object]]]:
    text = strip_comments(text)
    packs: Dict[str, List[Dict[str, object]]] = {}

    for match in LOOTPACK_PATTERN.finditer(text):
        pack_name = match.group(1)
        body, _ = extract_balanced_block(text, match.end() - 1, "(", ")")
        entries: List[Dict[str, object]] = []

        search_index = 0
        while True:
            entry_index = body.find("new LootPackEntry(", search_index)
            if entry_index < 0:
                break

            arguments, next_index = extract_balanced_block(body, entry_index + len("new LootPackEntry"), "(", ")")
            search_index = next_index
            parts = split_top_level_arguments(arguments)
            if len(parts) < 4:
                continue

            entries.append(
                {
                    "atSpawn": parts[0].strip().lower() == "true",
                    "arrayName": parts[1].replace("LootPack.", "").strip(),
                    "chancePercent": float(parts[2]),
                    "quantity": parts[3].strip(),
                }
            )

        packs[pack_name] = entries

    return packs


def parse_loot_pack_aliases(text: str, pack_names: Sequence[str]) -> Dict[str, str]:
    text = strip_comments(text)
    aliases: Dict[str, str] = {}

    for match in LOOTPACK_ALIAS_PATTERN.finditer(text):
        alias_name = match.group(1)
        expression = match.group(2)
        candidates = re.findall(r"\b([A-Z][A-Za-z0-9_]*)\b", expression)
        for candidate in candidates:
            if candidate in pack_names:
                aliases[alias_name] = candidate
                break

    return aliases


def common_reference_for_types(
    type_names: Sequence[str],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
    context: str,
) -> Dict[str, object] | None:
    resolved_references = [
        try_resolve_reference(type_name, item_index, tag_index, report, context) for type_name in type_names
    ]

    if any(reference is None for reference in resolved_references):
        return None

    normalized = {tuple(sorted(reference.items())) for reference in resolved_references if reference is not None}
    if len(normalized) == 1:
        return dict(next(iter(normalized)))

    exact_tags = {
        "armor": {"armor"},
        "clothing": {"clothing"},
        "gems": {"gems"},
        "jewels": {"jewels"},
        "skill items": {"skill items"},
        "shields": {"shields"},
        "weapons": {"weapons"},
    }
    reference_keys = {next(iter(reference.items())) for reference in resolved_references if reference is not None}

    for tag_name, accepted_tags in exact_tags.items():
        if all(key == ("itemTag", next(iter(accepted_tags))) for key in reference_keys):
            return {"itemTag": tag_name}

    return None


def build_entry(reference: Dict[str, object], chance: float, quantity: str) -> Dict[str, object]:
    entry = dict(reference)
    entry["chance"] = round(chance, 4)

    quantity_range = parse_dice_range(quantity)
    apply_quantity_range(entry, quantity_range)

    return entry


def apply_quantity_range(entry: Dict[str, object], quantity_range) -> None:
    if quantity_range.minimum == quantity_range.maximum:
        entry["amount"] = quantity_range.minimum
    else:
        entry["amountMin"] = quantity_range.minimum
        entry["amountMax"] = quantity_range.maximum


def try_build_weighted_loot_pack_table(
    pack_name: str,
    pack_entries: Sequence[Dict[str, object]],
    arrays: Dict[str, List[Dict[str, object]]],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
) -> Dict[str, object] | None:
    if len(pack_entries) != 1:
        return None

    pack_entry = pack_entries[0]
    if pack_entry["atSpawn"] or float(pack_entry["chancePercent"]) != 100.0:
        return None

    quantity_range = parse_dice_range(str(pack_entry["quantity"]))
    if quantity_range.minimum != quantity_range.maximum:
        return None

    array_name = str(pack_entry["arrayName"])
    item_array = arrays.get(array_name)
    if not item_array or len(item_array) <= 1:
        return None

    weighted_entries: List[Dict[str, object]] = []
    seen = set()
    context = f"loot_pack.{normalize_identifier(pack_name)}"

    for item in item_array:
        reference = try_resolve_reference(
            str(item["typeName"]),
            item_index,
            tag_index,
            report,
            f"{context} via {array_name}",
        )
        if reference is None:
            continue

        frozen = tuple(sorted(reference.items()))
        if frozen in seen:
            continue

        seen.add(frozen)
        entry = dict(reference)
        entry["weight"] = int(item["weight"])
        apply_quantity_range(entry, quantity_range)
        weighted_entries.append(entry)

    if not weighted_entries:
        append_warning(report, "skipped", f"{context}: unresolved weighted array {array_name}")
        return None

    return {
        "type": "loot",
        "id": context,
        "name": pack_name,
        "category": "loot",
        "description": "",
        "mode": "weighted",
        "rolls": 1,
        "noDropWeight": 0,
        "entries": weighted_entries,
    }


def expand_loot_pack_entries(
    pack_name: str,
    packs: Dict[str, List[Dict[str, object]]],
    arrays: Dict[str, List[Dict[str, object]]],
    aliases: Dict[str, str],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
    context: str,
    include_at_spawn: bool = False,
) -> List[Dict[str, object]]:
    resolved_pack_name = aliases.get(pack_name, pack_name)
    pack_entries = packs.get(resolved_pack_name)
    if not pack_entries:
        append_warning(report, "unmappedPatterns", f"{context}: missing pack {pack_name}")
        return []

    generated: List[Dict[str, object]] = []
    for pack_entry in pack_entries:
        if pack_entry["atSpawn"] and not include_at_spawn:
            continue

        array_name = str(pack_entry["arrayName"])
        item_array = arrays.get(array_name)
        if not item_array:
            append_warning(report, "unmappedPatterns", f"{context}: missing pack array {array_name}")
            continue

        if len(item_array) == 1:
            reference = try_resolve_reference(
                str(item_array[0]["typeName"]),
                item_index,
                tag_index,
                report,
                f"{context} via {resolved_pack_name}",
            )
        else:
            reference = common_reference_for_types(
                [str(item["typeName"]) for item in item_array],
                item_index,
                tag_index,
                report,
                f"{context} via {resolved_pack_name}",
            )

        if reference is None:
            append_warning(report, "skipped", f"{context}: unresolved pack {resolved_pack_name}")
            continue

        generated.append(
            build_entry(
                reference,
                float(pack_entry["chancePercent"]) / 100.0,
                str(pack_entry["quantity"]),
            )
        )

    return generated


def build_loot_pack_tables(
    packs: Dict[str, List[Dict[str, object]]],
    arrays: Dict[str, List[Dict[str, object]]],
    aliases: Dict[str, str],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
) -> List[Dict[str, object]]:
    tables: List[Dict[str, object]] = []

    for pack_name in sorted({*packs.keys(), *aliases.keys()}):
        resolved_pack_name = aliases.get(pack_name, pack_name)
        pack_entries = packs.get(resolved_pack_name)
        if not pack_entries:
            append_warning(report, "unmappedPatterns", f"loot_pack.{normalize_identifier(pack_name)}: missing pack")
            continue

        weighted_table = try_build_weighted_loot_pack_table(
            pack_name,
            pack_entries,
            arrays,
            item_index,
            tag_index,
            report,
        )
        if weighted_table is not None:
            tables.append(weighted_table)
            append_warning(report, "generated", weighted_table["id"])
            continue

        entries = expand_loot_pack_entries(
            pack_name,
            packs,
            arrays,
            aliases,
            item_index,
            tag_index,
            report,
            f"loot_pack.{normalize_identifier(pack_name)}",
            include_at_spawn=True,
        )
        if not entries:
            append_warning(report, "skipped", f"loot_pack.{normalize_identifier(pack_name)}: no supported entries")
            continue

        table_id = build_loot_id(pack_name, "loot_pack")
        tables.append(
            {
                "type": "loot",
                "id": table_id,
                "name": pack_name,
                "category": "loot",
                "description": "",
                "mode": "additive",
                "entries": entries,
            }
        )
        append_warning(report, "generated", table_id)

    return tables


def parse_generate_loot_tables(
    mobiles_root: Path,
    packs: Dict[str, List[Dict[str, object]]],
    pack_arrays: Dict[str, List[Dict[str, object]]],
    pack_aliases: Dict[str, str],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
) -> List[Dict[str, object]]:
    tables: List[Dict[str, object]] = []

    for path in sorted(mobiles_root.rglob("*.cs")):
        text = strip_comments(path.read_text(encoding="utf-8"))
        class_match = CREATURE_CLASS_PATTERN.search(text)
        generate_match = GENERATE_LOOT_PATTERN.search(text)
        if class_match is None or generate_match is None:
            continue

        class_name = class_match.group(1)
        body, _ = extract_balanced_block(text, generate_match.end() - 1, "{", "}")
        entries: List[Dict[str, object]] = []
        context = f"creature.{class_name}"

        for add_loot_match in re.finditer(r"AddLoot\(LootPack\.(\w+)(?:,\s*(\d+))?\);", body):
            pack_name = add_loot_match.group(1)
            repeat_count = int(add_loot_match.group(2) or "1")
            for _ in range(repeat_count):
                entries.extend(
                    expand_loot_pack_entries(
                        pack_name,
                        packs,
                        pack_arrays,
                        pack_aliases,
                        item_index,
                        tag_index,
                        report,
                        context,
                    )
                )

        for pack_gold_range in re.finditer(r"PackGold\((\d+),\s*(\d+)\);", body):
            entries.append(
                {
                    "itemTemplateId": "gold",
                    "chance": 1.0,
                    "amountMin": int(pack_gold_range.group(1)),
                    "amountMax": int(pack_gold_range.group(2)),
                }
            )

        for pack_gold_fixed in re.finditer(r"PackGold\((\d+)\);", body):
            entries.append({"itemTemplateId": "gold", "chance": 1.0, "amount": int(pack_gold_fixed.group(1))})

        for pack_gem_range in re.finditer(r"PackGem\((\d+),\s*(\d+)\);", body):
            entries.append(
                {
                    "itemTag": "gems",
                    "chance": 1.0,
                    "amountMin": int(pack_gem_range.group(1)),
                    "amountMax": int(pack_gem_range.group(2)),
                }
            )

        for pack_reg_range in re.finditer(r"PackReg\((\d+),\s*(\d+)\);", body):
            reference = try_resolve_reference("Reagent", item_index, tag_index, report, context)
            if reference is None:
                append_warning(report, "skipped", f"{context}: unresolved PackReg range")
                continue

            entry = dict(reference)
            entry["chance"] = 1.0
            entry["amountMin"] = int(pack_reg_range.group(1))
            entry["amountMax"] = int(pack_reg_range.group(2))
            entries.append(entry)

        for pack_reg_fixed in re.finditer(r"PackReg\((\d+)\);", body):
            reference = try_resolve_reference("Reagent", item_index, tag_index, report, context)
            if reference is None:
                append_warning(report, "skipped", f"{context}: unresolved PackReg")
                continue

            entry = dict(reference)
            entry["chance"] = 1.0
            entry["amount"] = int(pack_reg_fixed.group(1))
            entries.append(entry)

        for pack_item_match in PACK_ITEM_CONSTRUCTOR_PATTERN.finditer(body):
            type_name = pack_item_match.group(1)
            amount = int(pack_item_match.group(2) or "1")
            reference = try_resolve_reference(type_name, item_index, tag_index, report, context)
            if reference is None:
                append_warning(report, "skipped", f"{context}: unresolved PackItem {type_name}")
                continue

            entry = dict(reference)
            entry["chance"] = 1.0
            entry["amount"] = amount
            entries.append(entry)

        for pack_item_match in PACK_ITEM_SEED_FACTORY_PATTERN.finditer(body):
            reference = try_resolve_reference("Seed", item_index, tag_index, report, context)
            if reference is None:
                append_warning(report, "skipped", f"{context}: unresolved PackItem Seed")
                continue

            entry = dict(reference)
            entry["chance"] = 1.0
            entry["amount"] = 1
            entries.append(entry)

        for pack_item_match in PACK_ITEM_RANDOM_REAGENT_PATTERN.finditer(body):
            reference = try_resolve_reference("Reagent", item_index, tag_index, report, context)
            if reference is None:
                append_warning(report, "skipped", f"{context}: unresolved PackItem reagent")
                continue

            entry = dict(reference)
            entry["chance"] = 1.0
            entry["amount"] = 1
            entries.append(entry)

        if not entries:
            append_warning(report, "skipped", f"{context}: no supported GenerateLoot patterns")
            continue

        table_id = build_loot_id(class_name, "creature")
        tables.append(
            {
                "type": "loot",
                "id": table_id,
                "name": class_name,
                "category": "loot",
                "description": "",
                "mode": "additive",
                "entries": entries,
            }
        )
        append_warning(report, "generated", table_id)

    return tables


def resolve_fillable_reference(
    raw_argument: str,
    type_arrays: Dict[str, List[str]],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
    context: str,
) -> Dict[str, object] | None:
    type_match = re.fullmatch(r"typeof\((\w+)\)", raw_argument.strip())
    if type_match is not None:
        return try_resolve_reference(type_match.group(1), item_index, tag_index, report, context)

    loot_array_match = re.fullmatch(r"Loot\.(\w+)", raw_argument.strip())
    if loot_array_match is None:
        append_warning(report, "unmappedPatterns", f"{context}: unsupported fillable ref {raw_argument}")
        return None

    array_name = loot_array_match.group(1)
    type_names = type_arrays.get(array_name)
    if not type_names:
        append_warning(report, "unmappedPatterns", f"{context}: missing loot array {array_name}")
        return None

    return common_reference_for_types(type_names, item_index, tag_index, report, context)


def resolve_fillable_beverage_reference(
    container_type_name: str,
    beverage_type_name: str,
    item_index: Dict[str, Dict[str, object]],
    report: Dict[str, object],
    context: str,
) -> Dict[str, object] | None:
    beverage_pairs = {
        ("Pitcher", "Milk"): "pitcher_milk",
        ("BeverageBottle", "Ale"): "bottle_ale",
        ("BeverageBottle", "Wine"): "bottle_wine",
        ("BeverageBottle", "Liquor"): "bottle_liquor",
        ("Jug", "Cider"): "pitcher_cider",
    }

    item_template_id = beverage_pairs.get((container_type_name, beverage_type_name))
    if item_template_id is None:
        append_warning(
            report,
            "unmappedPatterns",
            f"{context}: unsupported beverage {container_type_name}/{beverage_type_name}",
        )
        return None

    if item_template_id not in item_index:
        append_warning(report, "missingItems", f"{context}: {item_template_id}")
        return None

    return {"itemTemplateId": item_template_id}


def parse_fillable_content_tables(
    file_path: Path,
    type_arrays: Dict[str, List[str]],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
) -> List[Dict[str, object]]:
    text = file_path.read_text(encoding="utf-8")
    tables: List[Dict[str, object]] = []

    for match in FILLABLE_CONTENT_PATTERN.finditer(text):
        content_name = match.group(1)
        body, _ = extract_balanced_block(text, match.end() - 1, "(", ")")
        parts = split_top_level_arguments(body)
        if len(parts) < 3:
            append_warning(report, "skipped", f"fillable.{content_name}: malformed content block")
            continue

        level = int(parts[0])
        entries_block = parts[2]
        if not entries_block.startswith("["):
            append_warning(report, "skipped", f"fillable.{content_name}: missing entry list")
            continue

        entries_body, _ = extract_balanced_block(entries_block, 0, "[", "]")
        weighted_entries: List[Dict[str, object]] = []
        search_index = 0
        while True:
            entry_index = entries_body.find("new Fillable", search_index)
            if entry_index < 0:
                break

            if entries_body.startswith("new FillableBvrge(", entry_index):
                constructor_name = "FillableBvrge"
                constructor_offset = len("new FillableBvrge")
            else:
                constructor_name = "FillableEntry"
                constructor_offset = len("new FillableEntry")

            arguments, next_index = extract_balanced_block(entries_body, entry_index + constructor_offset, "(", ")")
            search_index = next_index
            args = split_top_level_arguments(arguments)
            if not args:
                continue

            if constructor_name == "FillableBvrge":
                weight = 1
                ref_argument = args[0]
                if len(args) == 3:
                    weight = int(args[0])
                    ref_argument = args[1]
            else:
                weight = 1
                ref_argument = args[0]
                if len(args) >= 2:
                    try:
                        weight = int(args[0])
                        ref_argument = args[1]
                    except ValueError:
                        weight = 1
                        ref_argument = args[0]

            if constructor_name == "FillableBvrge":
                type_match = re.fullmatch(r"typeof\((\w+)\)", ref_argument.strip())
                beverage_match = re.fullmatch(r"BeverageType\.(\w+)", args[-1].strip())
                if type_match is None or beverage_match is None:
                    append_warning(
                        report,
                        "unmappedPatterns",
                        f"fillable.{content_name}: malformed beverage entry {arguments}",
                    )
                    continue

                reference = resolve_fillable_beverage_reference(
                    type_match.group(1),
                    beverage_match.group(1),
                    item_index,
                    report,
                    f"fillable.{content_name}",
                )
            else:
                reference = resolve_fillable_reference(
                    ref_argument,
                    type_arrays,
                    item_index,
                    tag_index,
                    report,
                    f"fillable.{content_name}",
                )

            if reference is None:
                continue

            weighted_entry = dict(reference)
            weighted_entry["weight"] = weight
            weighted_entry["amount"] = 1
            weighted_entries.append(weighted_entry)

        if not weighted_entries:
            append_warning(report, "skipped", f"fillable.{content_name}: no supported entries")
            continue

        table_id = build_loot_id(content_name, "fillable")
        tables.append(
            {
                "type": "loot",
                "id": table_id,
                "name": content_name,
                "category": "loot",
                "description": "",
                "mode": "weighted",
                "rolls": max(level, 1),
                "noDropWeight": 0,
                "entries": weighted_entries,
            }
        )
        append_warning(report, "generated", table_id)

    return tables


def build_uniform_weighted_entries(
    reference_type_names: Sequence[str],
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
    context: str,
    amount: int = 1,
) -> List[Dict[str, object]]:
    entries: List[Dict[str, object]] = []
    seen = set()
    for type_name in reference_type_names:
        reference = try_resolve_reference(type_name, item_index, tag_index, report, context)
        if reference is None:
            continue

        frozen = tuple(sorted(reference.items()))
        if frozen in seen:
            continue
        seen.add(frozen)

        entry = dict(reference)
        entry["weight"] = 1
        entry["amount"] = amount
        entries.append(entry)

    return entries


def build_treasure_chest_tables(
    treasure_map_file: Path,
    item_index: Dict[str, Dict[str, object]],
    tag_index: Dict[str, List[str]],
    report: Dict[str, object],
) -> List[Dict[str, object]]:
    text = treasure_map_file.read_text(encoding="utf-8")
    if "public static void Fill" not in text:
        append_warning(report, "skipped", "treasure_map: Fill method not found")
        return []

    tables: List[Dict[str, object]] = []

    tables.append(
        {
            "type": "loot",
            "id": "treasure_map.level_0",
            "name": "Treasure Map Level 0",
            "category": "loot",
            "description": "",
            "mode": "additive",
            "entries": [
                {"itemTemplateId": "gold", "chance": 1.0, "amountMin": 50, "amountMax": 100},
                {"itemTemplateId": "treasure_map", "chance": 0.75, "amount": 1},
            ],
        }
    )
    append_warning(report, "generated", "treasure_map.level_0")

    regular_reagents = [
        "black_pearl",
        "bloodmoss",
        "garlic",
        "ginseng",
        "mandrake_root",
        "nightshade",
        "spiders_silk",
        "sulfurous_ash",
    ]
    gem_types = [
        "amber",
        "amethyst",
        "citrine",
        "diamond",
        "emerald",
        "ruby",
        "sapphire",
        "star_sapphire",
        "tourmaline",
    ]

    for level in range(1, 7):
        tables.append(
            {
                "type": "loot",
                "id": f"treasure_map.level_{level}.gold",
                "name": f"Treasure Map Level {level} Gold",
                "category": "loot",
                "description": "",
                "mode": "additive",
                "entries": [{"itemTemplateId": "gold", "chance": 1.0, "amount": level * 1000}],
            }
        )
        append_warning(report, "generated", f"treasure_map.level_{level}.gold")

        scroll_entry = {"itemTag": "skill items", "weight": 1, "amount": 1}
        tables.append(
            {
                "type": "loot",
                "id": f"treasure_map.level_{level}.scrolls",
                "name": f"Treasure Map Level {level} Scrolls",
                "category": "loot",
                "description": "",
                "mode": "weighted",
                "rolls": level * 5,
                "noDropWeight": 0,
                "entries": [scroll_entry],
            }
        )
        append_warning(report, "generated", f"treasure_map.level_{level}.scrolls")

        reagent_entries = build_uniform_weighted_entries(
            regular_reagents,
            item_index,
            tag_index,
            report,
            f"treasure_map.level_{level}.reagents",
            amount=50,
        )
        if reagent_entries:
            tables.append(
                {
                    "type": "loot",
                    "id": f"treasure_map.level_{level}.reagents",
                    "name": f"Treasure Map Level {level} Reagents",
                    "category": "loot",
                    "description": "",
                    "mode": "weighted",
                    "rolls": level * 3,
                    "noDropWeight": 0,
                    "entries": reagent_entries,
                }
            )
            append_warning(report, "generated", f"treasure_map.level_{level}.reagents")

        gem_entries = build_uniform_weighted_entries(
            gem_types,
            item_index,
            tag_index,
            report,
            f"treasure_map.level_{level}.gems",
        )
        if gem_entries:
            tables.append(
                {
                    "type": "loot",
                    "id": f"treasure_map.level_{level}.gems",
                    "name": f"Treasure Map Level {level} Gems",
                    "category": "loot",
                    "description": "",
                    "mode": "weighted",
                    "rolls": level * 3,
                    "noDropWeight": 0,
                    "entries": gem_entries,
                }
            )
            append_warning(report, "generated", f"treasure_map.level_{level}.gems")

    return tables


def sort_tables(tables: Iterable[Dict[str, object]]) -> List[Dict[str, object]]:
    return sorted(tables, key=lambda table: str(table["id"]))


def write_report(path: Path, report: Dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(f"{json.dumps(report, indent=2)}\n", encoding="utf-8")


def generate_from_paths(modernuo_root: Path, repo_root: Path) -> Dict[str, object]:
    ensure_loot_support_items(repo_root)
    item_index, tag_index = load_item_template_index(repo_root)
    report = build_report()

    lootpack_path = modernuo_root / "Projects/UOContent/Misc/LootPack.cs"
    loot_path = modernuo_root / "Projects/UOContent/Misc/Loot.cs"
    fillable_path = modernuo_root / "Projects/UOContent/Items/Containers/Fillable Containers/FillableContent.ContentTypes.cs"
    treasure_map_path = modernuo_root / "Projects/UOContent/Items/Containers/TreasureMapChest.cs"
    mobiles_root = modernuo_root / "Projects/UOContent/Mobiles"

    lootpack_text = lootpack_path.read_text(encoding="utf-8")
    loot_text = loot_path.read_text(encoding="utf-8")

    pack_arrays = parse_loot_pack_arrays(lootpack_text)
    packs = parse_loot_packs(lootpack_text)
    pack_aliases = parse_loot_pack_aliases(lootpack_text, list(packs.keys()))
    type_arrays = parse_type_arrays(loot_text)

    creatures = sort_tables(
        parse_generate_loot_tables(
            mobiles_root,
            packs,
            pack_arrays,
            pack_aliases,
            item_index,
            tag_index,
            report,
        )
    )
    loot_packs = sort_tables(build_loot_pack_tables(packs, pack_arrays, pack_aliases, item_index, tag_index, report))
    fillable = sort_tables(
        parse_fillable_content_tables(
            fillable_path,
            type_arrays,
            item_index,
            tag_index,
            report,
        )
    )
    treasure_chests = sort_tables(build_treasure_chest_tables(treasure_map_path, item_index, tag_index, report))

    output_paths = build_output_paths(repo_root)
    write_json_array(output_paths["creatures"], creatures)
    write_json_array(output_paths["treasure_chests"], treasure_chests)
    write_json_array(output_paths["fillable_containers"], fillable)
    write_json_array(output_paths["loot_packs"], loot_packs)
    write_report(repo_root / "artifacts/loot-import-report.json", report)

    return report


def main() -> int:
    args = build_argument_parser().parse_args()
    generate_from_paths(args.modernuo_root, args.repo_root)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
