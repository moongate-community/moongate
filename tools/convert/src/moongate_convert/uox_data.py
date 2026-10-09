"""The UOX3 converter's tables and the readers of UOX3's small files: the kind of weapon of a graphic, the corrections of UOX3's data, the dictionary, the
color lists, the skill tags and the script associations."""

from __future__ import annotations

from pathlib import Path

from . import dfn
from .guildmasters import SKILL_TYPES
from .specs import HueSpec
from .textutil import read_lines, snake_case, trim, try_int

WEAPON_TYPES_BY_NAME: dict[str, list[int]] = {
    "sword": [
        0x08FD, 0x0900, 0x0907, 0x0908, 0x090B, 0x090C, 0x0EC4, 0x0EC5,
        0x0F5E, 0x0F5F, 0x0F60, 0x0F61, 0x13B5, 0x13B6, 0x13B7, 0x13B8,
        0x13B9, 0x13BA, 0x13F6, 0x13F7, 0x13FE, 0x13FF, 0x143E, 0x143F,
        0x1440, 0x1441, 0x2554, 0x255E, 0x2560, 0x2573, 0x2574, 0x2575,
        0x2576, 0x2578, 0x257D, 0x257E, 0x26BB, 0x26BD, 0x26C1, 0x26C5,
        0x26C7, 0x26CB, 0x26CE, 0x26CF, 0x27A2, 0x27A4, 0x27A8, 0x27A9,
        0x27ED, 0x27EF, 0x27F3, 0x27F4, 0x2D26, 0x2D27, 0x2D29, 0x2D32,
        0x2D33, 0x2D35, 0x4068, 0x4071, 0x4073, 0x4074, 0x4075, 0x4076,
        0x48B6, 0x48B7, 0x48BA, 0x48BB, 0x48C6, 0x48C7, 0x48D0, 0x48D1,
        0xA28B, 0xA293, 0xA33B, 0xA33C, 0xA33D, 0xA33E, 0xA33F, 0xA340,
        0xA341, 0xA342, 0xA345, 0xA346,
    ],
    "fencing": [
        0x08FE, 0x0902, 0x0904, 0x0E87, 0x0E88, 0x0F51, 0x0F52, 0x0F62,
        0x0F63, 0x1400, 0x1401, 0x1402, 0x1403, 0x1404, 0x1405, 0x2558,
        0x2562, 0x2572, 0x257A, 0x257B, 0x257C, 0x26BE, 0x26BF, 0x26C0,
        0x26C8, 0x26C9, 0x26CA, 0x27A7, 0x27AB, 0x27AD, 0x27AF, 0x27F2,
        0x27F6, 0x27F8, 0x27FA, 0x2D20, 0x2D21, 0x2D22, 0x2D23, 0x2D2C,
        0x2D2D, 0x2D2E, 0x2D2F, 0x406A, 0x406D, 0x4072, 0x48BC, 0x48BD,
        0x48C8, 0x48C9, 0x48CA, 0x48CB, 0x48CE, 0x48CF, 0xA28A, 0xA292,
    ],
    "thrown": [
        0x08FF, 0x0901, 0x090A, 0x4067, 0x406B, 0x406C,
    ],
    "mace": [
        0x0903, 0x0905, 0x0906, 0x0DF0, 0x0DF1, 0x0DF2, 0x0DF3, 0x0DF4,
        0x0DF5, 0x0E81, 0x0E82, 0x0E89, 0x0E8A, 0x0F5C, 0x0F5D, 0x0FB4,
        0x0FB5, 0x13AF, 0x13B0, 0x13B3, 0x13B4, 0x13E3, 0x13E4, 0x13F4,
        0x13F5, 0x13F8, 0x13F9, 0x1406, 0x1407, 0x1438, 0x1439, 0x143A,
        0x143B, 0x143C, 0x143D, 0x2555, 0x2556, 0x2557, 0x2559, 0x255A,
        0x255C, 0x2561, 0x2565, 0x2566, 0x2568, 0x2569, 0x256B, 0x256C,
        0x256D, 0x256E, 0x256F, 0x257F, 0x26BC, 0x26C6, 0x27A3, 0x27A6,
        0x27AE, 0x27EE, 0x27F1, 0x27F9, 0x2D24, 0x2D25, 0x2D30, 0x2D31,
        0x406E, 0x406F, 0x4070, 0x48C0, 0x48C1, 0x48C2, 0x48C3, 0x48CC,
        0x48CD, 0xA289, 0xA291, 0xA343, 0xA344, 0xA347, 0xA348,
    ],
    "axe": [
        0x0E85, 0x0E86, 0x0EC2, 0x0EC3, 0x0F43, 0x0F44, 0x0F45, 0x0F46,
        0x0F47, 0x0F48, 0x0F49, 0x0F4A, 0x0F4B, 0x0F4C, 0x13FA, 0x13FB,
        0x1442, 0x1443, 0x255D, 0x255F, 0x2564, 0x2567, 0x2570, 0x2579,
        0x2D28, 0x2D34, 0x48AE, 0x48AF, 0x48B0, 0x48B1, 0x48B2, 0x48B3,
    ],
    "pole_arm": [
        0x0F4D, 0x0F4E, 0x255B, 0x2577, 0x26BA, 0x26C4, 0x48B4, 0x48B5,
        0x48C4, 0x48C5,
    ],
    "crossbow": [
        0x0F4F, 0x0F50, 0x13FC, 0x13FD, 0x26C3, 0x26CD,
    ],
    "bow": [
        0x13B1, 0x13B2, 0x2571, 0x26C2, 0x26CC, 0x27A5, 0x27AA, 0x27F0,
        0x27F5, 0x2D1E, 0x2D1F, 0x2D2A, 0x2D2B,
    ],
}


# The kind of weapon of a graphic, by UOX3's own ``GetWeaponType``; a graphic listed under two kinds is the last one's.
WEAPON_TYPE_BY_GRAPHIC: dict[int, str] = {graphic: kind for kind, graphics in WEAPON_TYPES_BY_NAME.items() for graphic in graphics}

# Mistakes in UOX3's own item data, corrected as the blocks are read: (header, tag) -> the corrected value.
DATA_FIXES: dict[tuple[str, str], str] = {
    # leather.dfn names the leather gloves (0x13c6) and a leather tunic (0x13cc) for these two.
    ("necro_sleeves", "get"): "0x13cd",
    ("necro_leggings", "get"): "0x13cb",
}

# UOX3 script (in the file associations) -> the Moongate item script doing the same.
SCRIPT_IDS: dict[str, str] = {"item/lights.js": "light", "skill/mining.js": "pickaxe", "item/sword.js": "blade"}


def apply_fixes(block: dfn.DfnBlock) -> None:
    """Corrects the known mistakes in UOX3's own data for a block's header, in its fields."""
    for (header, tag), value in DATA_FIXES.items():
        if header.lower() == block.header.lower():
            block.fields[tag] = value


def load_dictionary(path: Path) -> dict[int, str]:
    """The texts of a UOX3 ``dictionary.ENG`` by id (``id=text`` lines), which numeric NPC names, titles and name list entries refer to;
    a missing file gives none."""
    texts: dict[int, str] = {}

    if not path.is_file():
        return texts

    for line in read_lines(path):
        separator = line.find("=")

        if separator > 0 and (number := try_int(trim(line[:separator]))) is not None:
            texts[number] = trim(line[separator + 1 :])

    return texts


_COLOR_PREFIX = "RANDOMCOLOR "


def load_color_lists(path: Path) -> dict[int, HueSpec | None]:
    """The ``[RANDOMCOLOR n]`` lists of UOX3's colors.dfn: a list of consecutive hues is a range, anything else is None (no color)."""
    lists: dict[int, HueSpec | None] = {}

    if not path.is_file():
        return lists

    for block in dfn.parse(read_lines(path)):
        if block.header[: len(_COLOR_PREFIX)].lower() != _COLOR_PREFIX.lower():
            continue

        number = try_int(block.header[len(_COLOR_PREFIX) :])

        if number is None:
            continue

        hues: list[int] = []

        for entry in block.entries:
            hue = dfn.uox_number(entry)

            if hue is None:
                hues.clear()

                break

            hues.append(hue)

        lists[number] = _to_range(hues)

    return lists


def _to_range(hues: list[int]) -> HueSpec | None:
    if not hues:
        return None

    low, high = min(hues), max(hues)

    if len(set(hues)) != high - low + 1:
        return None

    if low == high:
        return HueSpec.from_value(low) if 0 <= low <= 0xFFFF else None

    return HueSpec.try_parse(f"0x{low:04X}-0x{high:04X}")


# Tags whose name is not the SkillType name without underscores.
_SKILL_ALIASES = {
    "BLACKSMITHING": "Blacksmithy",
    "BOWCRAFT": "BowcraftFletching",
    "ENTICEMENT": "Discordance",
    "EVALUATINGINTEL": "EvaluatingIntelligence",
    "FORENSICS": "ForensicEvaluation",
    "MAGICRESISTANCE": "ResistingSpells",
    "TAMING": "AnimalTaming",
    "TASTEID": "TasteIdentification",
    "ITEMID": "ItemIdentification",
}
_SKILL_BY_NAME = {skill.lower(): skill for skill in SKILL_TYPES}


def map_skill(uox_tag: str) -> str | None:
    """The server's name for the skill of a UOX3 tag (``TAMING`` is ``AnimalTaming``), None for a tag that is no skill."""
    alias = {tag.lower(): skill for tag, skill in _SKILL_ALIASES.items()}.get(uox_tag.lower())

    return alias or _SKILL_BY_NAME.get(uox_tag.lower())


class ScriptAssociations:
    """Which Moongate item script does what a UOX3 script does, from UOX3's ``jse_fileassociations.scp`` and ``jse_objectassociations.scp``."""

    FILE_ASSOCIATIONS = "jse_fileassociations.scp"
    OBJECT_ASSOCIATIONS = "jse_objectassociations.scp"

    def __init__(self, files: dict[int, str], by_graphic: dict[int, int]) -> None:
        self._files = files
        self._by_graphic = by_graphic

    @staticmethod
    def load(directory: Path) -> ScriptAssociations:
        """Reads the two files of a UOX3 ``js`` folder; a missing one is a FileNotFoundError that says which."""
        files: dict[int, str] = {}
        by_graphic: dict[int, int] = {}

        for section, key, value in _read_entries(directory / ScriptAssociations.FILE_ASSOCIATIONS):
            number = dfn.uox_number(key)

            if section == "SCRIPT_LIST" and number is not None:
                files[number] = value.replace("\\", "/")

        for section, key, value in _read_entries(directory / ScriptAssociations.OBJECT_ASSOCIATIONS):
            graphic, number = dfn.uox_number(key), dfn.uox_number(value)

            if section == "ENVOKE" and graphic is not None and number is not None:
                by_graphic[graphic] = number

        return ScriptAssociations(files, by_graphic)

    def script_id_for(self, block: dfn.DfnBlock, graphic: int) -> str | None:
        """The Moongate script of an item block: the one its own ``script=`` names, else the one UOX3 gives its graphic."""
        own = dfn.uox_number(block.fields["script"]) if "script" in block.fields else None

        if own is not None:
            number = own
        elif graphic in self._by_graphic:
            number = self._by_graphic[graphic]
        else:
            return None

        file = self._files.get(number)

        return None if file is None else {name.lower(): script for name, script in SCRIPT_IDS.items()}.get(file.lower())


def _read_entries(path: Path) -> list[tuple[str, str, str]]:
    if not path.is_file():
        raise FileNotFoundError(f"{path.name} is missing from the scripts source.")

    entries: list[tuple[str, str, str]] = []
    section = ""

    for raw in read_lines(path):
        line = trim(raw)

        if line.startswith("[") and line.endswith("]"):
            section = trim(line[1:-1])
        elif not line.startswith("//") and (equals := line.find("=")) > 0:
            entries.append((section, trim(line[:equals]), trim(line[equals + 1 :])))

    return entries
