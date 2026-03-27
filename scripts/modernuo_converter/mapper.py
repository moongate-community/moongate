"""Transform parsed ModernUO NPC data into Moongate template format."""

import json
import re
from pathlib import Path
from typing import Optional, Union

from .constants import AI_TYPE_TO_BRAIN, LOOT_PACK_MAP, RESISTANCE_TYPE_MAP

TEMPLATE_BRAIN_OVERRIDES = {
    "banker_npc": "town_banker",
}
DEFAULT_FIGHT_MODE = "closest"
DEFAULT_RANGE_PERCEPTION = 16
DEFAULT_RANGE_FIGHT = 1
VENDOR_DEFAULT_FIGHT_MODE = "none"
VENDOR_DEFAULT_RANGE_PERCEPTION = 2


def _pascal_to_snake(name: str) -> str:
    """Convert PascalCase to snake_case."""
    s = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", name)
    s = re.sub(r"([a-z\d])([A-Z])", r"\1_\2", s)
    return s.lower()


def _avg(min_val: int, max_val: int) -> int:
    """Return the integer average of two values."""
    return (min_val + max_val) // 2


def _avg_float(min_val: float, max_val: float) -> float:
    """Return the average of two float values."""
    return (min_val + max_val) / 2.0


def _body_hex(body_int: int) -> str:
    """Format body ID as hex string 0xNNNN."""
    return f"0x{body_int:04X}"


def _canonicalize_fight_mode(value) -> Optional[str]:
    if value is None:
        return None

    if not isinstance(value, str):
        return None

    stripped = value.strip()
    if not stripped:
        return None

    match = re.search(r"(?:FightMode\.)?(\w+)$", stripped)
    if match is None:
        return None

    return match.group(1).lower()


def _canonicalize_fixed_hue_value(value):
    if value is None:
        return None

    if isinstance(value, int):
        return f"0x{value:04X}"

    if not isinstance(value, str):
        return None

    stripped = value.strip()
    if not stripped:
        return None

    if stripped.startswith("hue("):
        return stripped

    if re.fullmatch(r"0x[0-9A-Fa-f]+", stripped):
        return f"0x{int(stripped, 16):04X}"

    if re.fullmatch(r"\d+", stripped):
        return f"0x{int(stripped):04X}"

    return None


def _canonicalize_skin_hue_value(value):
    """Normalize supported skin hue helpers to canonical template values."""
    fixed_value = _canonicalize_fixed_hue_value(value)
    if fixed_value is not None:
        return fixed_value

    if not isinstance(value, str):
        return None

    stripped = value.strip()

    if stripped == "Race.Human.RandomSkinHue()":
        return "hue(1002:1058)"

    return None


def _canonicalize_appearance_hue_value(value):
    return _canonicalize_fixed_hue_value(value)


def _canonicalize_item_hue_value(value):
    """Normalize supported item hue helpers to canonical template values."""
    fixed_value = _canonicalize_fixed_hue_value(value)
    if fixed_value is not None:
        return fixed_value

    if not isinstance(value, str):
        return None

    stripped = value.strip()

    if stripped == "Utility.RandomNeutralHue()":
        return "hue(1801:1908)"

    return None


def _normalize_item_catalog_key(item_class_name: str) -> str:
    return _pascal_to_snake(item_class_name)


def _lookup_item_metadata(item_catalog: dict, item_class_name: str) -> Optional[dict]:
    if not item_catalog:
        return None

    catalog_key = _normalize_item_catalog_key(item_class_name)
    metadata = item_catalog.get(catalog_key)
    if isinstance(metadata, dict):
        return metadata

    for key, value in item_catalog.items():
        if isinstance(key, str) and key.lower() == catalog_key and isinstance(value, dict):
            return value

    return None


def _resolve_equipment_option(option: dict, item_catalog: dict) -> Optional[dict]:
    class_name = option.get("class_name")
    if not isinstance(class_name, str) or not class_name:
        return None

    metadata = _lookup_item_metadata(item_catalog, class_name)
    if metadata is None:
        return None

    layer = metadata.get("layer")
    if not isinstance(layer, str) or not layer:
        return None

    option_data = {
        "itemTemplateId": _normalize_item_catalog_key(class_name),
        "layer": layer,
        "weight": option.get("weight", 1),
    }

    hue = _canonicalize_item_hue_value(option.get("hue"))
    if hue is not None:
        option_data["hue"] = hue

    return option_data


def _build_equipment_entry(group: list, item_catalog: dict) -> Optional[dict]:
    resolved = []
    for option in group:
        resolved_option = _resolve_equipment_option(option, item_catalog)
        if resolved_option is None:
            return None
        resolved.append(resolved_option)

    if not resolved:
        return None

    layers = {option["layer"] for option in resolved}
    if len(layers) != 1:
        return None

    layer = resolved[0]["layer"]
    if len(resolved) == 1:
        entry = {"layer": layer, "itemTemplateId": resolved[0]["itemTemplateId"]}
        if "hue" in resolved[0]:
            entry["hue"] = resolved[0]["hue"]
        return entry

    items = []
    for option in resolved:
        item_entry = {
            "itemTemplateId": option["itemTemplateId"],
            "weight": option.get("weight", 1),
        }
        if "hue" in option:
            item_entry["hue"] = option["hue"]
        items.append(item_entry)

    return {"layer": layer, "items": items}


def _resolve_equipment_group(group: list, item_catalog: dict) -> Optional[list[dict]]:
    resolved = []
    for option in group:
        resolved_option = _resolve_equipment_option(option, item_catalog)
        if resolved_option is None:
            return None
        resolved.append(resolved_option)

    return resolved or None


def _collapse_equipment_entries(entries: list[dict]) -> list[dict]:
    last_index_by_layer = {}
    for index, entry in enumerate(entries):
        last_index_by_layer[entry["layer"]] = index

    return [
        entry
        for index, entry in enumerate(entries)
        if last_index_by_layer.get(entry["layer"]) == index
    ]


def _build_equipment_variants(groups: list, item_catalog: dict) -> list[list[dict]]:
    variants = [[]]
    for group in groups or []:
        if not isinstance(group, list):
            continue

        resolved_group = _resolve_equipment_group(group, item_catalog)
        if resolved_group is None:
            continue

        layers = {option["layer"] for option in resolved_group}
        if len(layers) == 1:
            entry = _build_equipment_entry(group, item_catalog)
            if entry is None:
                continue

            for variant_entries in variants:
                variant_entries.append(entry)

            continue

        next_variants = []
        for variant_entries in variants:
            for option in resolved_group:
                entry = {
                    "layer": option["layer"],
                    "itemTemplateId": option["itemTemplateId"],
                }
                if "hue" in option:
                    entry["hue"] = option["hue"]
                next_variants.append([*variant_entries, entry])

        variants = next_variants

    if not variants:
        return [[]]

    return [_collapse_equipment_entries(entries) for entries in variants]


def _variant_appearance(parsed: dict, variant: dict) -> dict:
    appearance_data = dict(variant.get("appearance", {}))
    appearance = {}

    body_value = appearance_data.get("body")
    if body_value is None:
        body_value = parsed.get("body")
    if body_value is not None:
        appearance["body"] = _body_hex(int(body_value))

    skin_hue_value = appearance_data.get("skinHue")
    if skin_hue_value is None:
        skin_hue_value = appearance_data.get("skin_hue")
    if skin_hue_value is None:
        skin_hue_value = parsed.get("skin_hue")

    canonical_skin_hue = _canonicalize_skin_hue_value(skin_hue_value)
    if canonical_skin_hue is not None:
        appearance["skinHue"] = canonical_skin_hue

    appearance_hue_field_map = {
        "hairHue": "hair_hue",
        "facialHairHue": "facial_hair_hue",
    }
    for field_name, parsed_key in appearance_hue_field_map.items():
        value = appearance_data.get(field_name)
        if value is None:
            value = appearance_data.get(parsed_key)
        if value is None:
            value = parsed.get(parsed_key)

        canonical_value = _canonicalize_appearance_hue_value(value)
        if canonical_value is not None:
            appearance[field_name] = canonical_value

    style_field_map = {
        "hairStyle": "hair_style",
        "facialHairStyle": "facial_hair_style",
    }
    for field_name, parsed_key in style_field_map.items():
        value = appearance_data.get(field_name)
        if value is None:
            value = appearance_data.get(parsed_key)
        if value is None:
            value = parsed.get(parsed_key)

        if value is not None:
            appearance[field_name] = int(value)

    return appearance


def _extract_body_choices(parsed: dict, variant: dict) -> list[Optional[int]]:
    appearance_data = dict(variant.get("appearance", {}))

    body_value = appearance_data.get("body")
    if body_value is None:
        body_value = parsed.get("body")
    if body_value is not None:
        return [int(body_value)]

    body_options = appearance_data.get("body_options")
    if body_options is None:
        body_options = parsed.get("body_options")
    if body_options:
        return [int(value) for value in body_options]

    return [None]


def _build_variants(parsed: dict, variant: dict, item_catalog: dict, shared_groups: list) -> list[dict]:
    groups = []
    groups.extend(shared_groups or [])
    groups.extend(variant.get("equipment_groups", []))

    equipment_variants = _build_equipment_variants(groups, item_catalog)
    body_choices = _extract_body_choices(parsed, variant)
    suffix_required = len(body_choices) > 1 or len(equipment_variants) > 1

    built_variants = []
    variant_index = 1
    for body_choice in body_choices:
        variant_data = {
            "name": variant.get("name", "default"),
            "weight": variant.get("weight", 1),
            "appearance": dict(variant.get("appearance", {})),
        }
        if body_choice is not None:
            variant_data["appearance"]["body"] = body_choice
            variant_data["appearance"].pop("body_options", None)

        for equipment in equipment_variants:
            built_variant = {
                "name": variant_data["name"],
                "weight": variant_data["weight"],
                "appearance": _variant_appearance(parsed, variant_data),
                "equipment": equipment,
            }
            if suffix_required:
                built_variant["name"] = f"{built_variant['name']}_{variant_index}"
                variant_index += 1
            built_variants.append(built_variant)

    return built_variants


def _notoriety_from_karma(karma: int) -> str:
    """Derive notoriety from karma value."""
    if karma < 0:
        return "CanBeAttacked"
    return "Innocent"


def _build_sounds(base_sound_id: int) -> dict:
    """Build sound map from BaseSoundID.

    ModernUO convention:
    - Idle = base
    - StartAttack / Attack = base + 1
    - Attack (hit) = base + 2
    - Defend (hurt) = base + 3
    - Die = base + 4
    """
    return {
        "Idle": base_sound_id,
        "StartAttack": base_sound_id + 1,
        "Attack": base_sound_id + 2,
        "Defend": base_sound_id + 3,
        "Die": base_sound_id + 4,
    }


def _build_skills(skills_raw: dict) -> dict:
    """Convert skill (min, max) pairs to Moongate format (average * 10, as int)."""
    result = {}
    for skill_name, (min_val, max_val) in skills_raw.items():
        avg = _avg_float(min_val, max_val)
        result[skill_name] = int(avg * 10)
    return result


def _build_resistances(resistances_raw: dict) -> dict:
    """Convert resistance (min, max) pairs to Moongate format (average)."""
    result = {}
    for res_type, (min_val, max_val) in resistances_raw.items():
        mapped_key = RESISTANCE_TYPE_MAP.get(res_type, res_type.lower())
        result[mapped_key] = _avg(min_val, max_val)
    return result


def _build_damage_types(damage_types_raw: dict) -> dict:
    """Convert damage type percentages to Moongate format."""
    result = {}
    for dmg_type, value in damage_types_raw.items():
        mapped_key = RESISTANCE_TYPE_MAP.get(dmg_type, dmg_type.lower())
        result[mapped_key] = value
    return result


def _build_loot_tables(parsed: dict) -> list:
    """Build loot table references from parsed loot data."""
    tables = []

    # Add loot pack references
    for loot_entry in parsed.get("loot", []):
        pack_name = loot_entry["pack"]
        count = loot_entry["count"]
        mapped = LOOT_PACK_MAP.get(pack_name)
        if mapped:
            for _ in range(count):
                tables.append(mapped)
        else:
            tables.append(f"loot_pack.{_pascal_to_snake(pack_name)}")

    # If there are pack_items, reference a creature-specific loot table
    if parsed.get("pack_items"):
        class_snake = _pascal_to_snake(parsed["class_name"])
        tables.append(f"creature.{class_snake}")

    return tables


def _build_tags(parsed: dict) -> list:
    """Build tags list from parsed data."""
    tags = []
    category = parsed.get("category", "")

    if category == "monsters":
        tags.append("monster")
    elif category == "animals":
        tags.append("animal")
    elif category == "vendors":
        tags.append("vendor")
        tags.append("npc")
    elif category == "town_npcs":
        tags.append("townfolk")
        tags.append("npc")

    if parsed.get("tamable"):
        tags.append("tamable")

    return tags


def _derive_category_label(parsed: dict) -> str:
    """Derive the category label for the template."""
    category = parsed.get("category", "")
    if category == "monsters":
        return "monster"
    elif category == "animals":
        return "animal"
    elif category in ("vendors", "town_npcs"):
        return "npc"
    return "mobile"


def _build_ai(parsed: dict, template_id: str) -> dict:
    ai_type = parsed.get("ai_type", "")
    category = parsed.get("category", "")

    if template_id in TEMPLATE_BRAIN_OVERRIDES:
        brain = TEMPLATE_BRAIN_OVERRIDES[template_id]
    elif ai_type and ai_type in AI_TYPE_TO_BRAIN:
        brain = AI_TYPE_TO_BRAIN[ai_type]
    elif category in ("vendors", "town_npcs"):
        brain = "ai_vendor"
    else:
        brain = "ai_melee"

    if category == "vendors":
        default_fight_mode = VENDOR_DEFAULT_FIGHT_MODE
        default_range_perception = VENDOR_DEFAULT_RANGE_PERCEPTION
    else:
        default_fight_mode = DEFAULT_FIGHT_MODE
        default_range_perception = DEFAULT_RANGE_PERCEPTION

    return {
        "brain": brain,
        "fightMode": _canonicalize_fight_mode(parsed.get("fight_mode")) or default_fight_mode,
        "rangePerception": int(parsed.get("range_perception", default_range_perception)),
        "rangeFight": int(parsed.get("range_fight", DEFAULT_RANGE_FIGHT)),
    }


def map_to_template(parsed: dict, item_catalog: Optional[dict] = None) -> dict:
    """Convert a parsed NPC dict to a Moongate mobile template dict."""
    if item_catalog is None:
        item_catalog = {}

    class_name = parsed["class_name"]
    template_id = _pascal_to_snake(class_name) + "_npc"

    template = {
        "type": "mobile",
        "id": template_id,
        "category": _derive_category_label(parsed),
        "description": f"Converted from ModernUO {class_name}.",
        "tags": _build_tags(parsed),
    }

    # Stats
    if "str_min" in parsed:
        template["strength"] = _avg(parsed["str_min"], parsed["str_max"])
    if "dex_min" in parsed:
        template["dexterity"] = _avg(parsed["dex_min"], parsed["dex_max"])
    if "int_min" in parsed:
        template["intelligence"] = _avg(parsed["int_min"], parsed["int_max"])
    if "hits_min" in parsed:
        template["hits"] = _avg(parsed["hits_min"], parsed["hits_max"])
        template["maxHits"] = _avg(parsed["hits_min"], parsed["hits_max"])
    if "mana_min" in parsed:
        template["mana"] = _avg(parsed["mana_min"], parsed["mana_max"])
    elif "int_min" in parsed:
        # Default mana = intelligence if not explicitly set
        pass
    if "stam_min" in parsed:
        template["stamina"] = _avg(parsed["stam_min"], parsed["stam_max"])

    # Damage
    if "damage_min" in parsed:
        template["minDamage"] = parsed["damage_min"]
        template["maxDamage"] = parsed["damage_max"]

    # Armor
    if "virtual_armor" in parsed:
        template["armorRating"] = parsed["virtual_armor"]

    # Fame / Karma / Notoriety
    if "fame" in parsed:
        template["fame"] = parsed["fame"]
    if "karma" in parsed:
        template["karma"] = parsed["karma"]
        template["notoriety"] = _notoriety_from_karma(parsed["karma"])

    template["ai"] = _build_ai(parsed, template_id)

    # Skills
    if "skills" in parsed:
        template["skills"] = _build_skills(parsed["skills"])

    # Loot tables
    loot_tables = _build_loot_tables(parsed)
    if loot_tables:
        template["lootTables"] = loot_tables

    # Sounds
    if "base_sound_id" in parsed:
        template["sounds"] = _build_sounds(parsed["base_sound_id"])

    # Resistances
    if "resistances" in parsed:
        template["resistances"] = _build_resistances(parsed["resistances"])

    # Damage types
    if "damage_types" in parsed:
        template["damageTypes"] = _build_damage_types(parsed["damage_types"])

    # Title / Name
    if "title" in parsed:
        template["title"] = parsed["title"]
    elif "name" in parsed:
        template["title"] = parsed["name"]

    shared_groups = parsed.get("shared_equipment_groups", [])
    parsed_variants = parsed.get("variants") or [
        {"name": "default", "weight": 1, "appearance": {}, "equipment_groups": []}
    ]
    built_variants = []
    for variant in parsed_variants:
        built_variants.extend(_build_variants(parsed, variant, item_catalog, shared_groups))
    template["variants"] = built_variants

    return template


def load_item_template_catalog(items_root: Union[Path, str]) -> dict:
    """Load item template metadata keyed by template id."""
    root = Path(items_root)
    catalog = {}

    if not root.exists():
        return catalog

    for path in sorted(root.rglob("*.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue

        if isinstance(data, dict):
            entries = [data]
        elif isinstance(data, list):
            entries = data
        else:
            continue

        for entry in entries:
            if not isinstance(entry, dict):
                continue

            template_id = entry.get("id") or entry.get("itemId")
            if not isinstance(template_id, str) or not template_id:
                continue

            catalog[template_id.lower()] = entry

    return catalog


def map_pack_items_to_loot(parsed: dict) -> Optional[dict]:
    """Create a creature-specific loot template from PackItem calls.

    Returns None if no pack items exist.
    """
    if not parsed.get("pack_items"):
        return None

    class_name = parsed["class_name"]
    class_snake = _pascal_to_snake(class_name)

    entries = []
    for item in parsed["pack_items"]:
        item_name = item["item"]

        if item_name == "Reagent":
            entry = {"itemTag": "reagents", "chance": 1.0, "amount": item["amount"]}
        elif item_name == "RandomTalisman":
            entry = {"itemTag": "talismans", "chance": 1.0, "amount": item["amount"]}
        else:
            entry = {
                "itemTemplateId": _pascal_to_snake(item_name),
                "chance": 1.0,
                "amount": item["amount"],
            }

        entries.append(entry)

    return {
        "type": "loot",
        "id": f"creature.{class_snake}",
        "name": class_name,
        "category": "loot",
        "description": f"Creature-specific loot for {class_name}.",
        "mode": "additive",
        "entries": entries,
    }


def map_vendor_to_sell_profile(parsed: dict) -> Optional[dict]:
    """Create a placeholder sell profile from vendor SBInfo references.

    Returns None if no SBInfo data exists.
    """
    if not parsed.get("sb_infos"):
        return None

    class_name = parsed["class_name"]
    class_snake = _pascal_to_snake(class_name)

    return {
        "type": "sell_profile",
        "id": f"vendor.{class_snake}",
        "name": f"{class_name} Vendor",
        "category": "vendors",
        "description": f"Placeholder sell profile for {class_name}. SBInfo sources: {', '.join(parsed['sb_infos'])}.",
        "vendorItems": [],
        "acceptedItems": [],
    }
