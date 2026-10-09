"""``modernuo-guildmasters``: the guildmasters of ModernUO (``Mobiles/Vendors/NPC/Guildmasters/*Guildmaster.cs``) into mobile templates.

For each class a man, ``m_<trade>_guildmaster``, and a woman, ``f_<trade>_guildmaster`` (UOX3's names), with the title, the skills (the C#
ranges as dice), the guild and the clothes of a vendor; and the npc list ``<trade>guildmaster`` that picks one of the two. Nothing is run:
the C# is read as syntax.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import TextIO

from . import csharp, tomlout
from .csharp import SourceError
from .report import ConversionReport
from .textutil import snake_case, write_text
from .vendors import era_base, items_by_graphic

MOBILES_FILE = "guildmasters.toml"
LISTS_FILE = "npclists_guildmasters.toml"
SUFFIX = "Guildmaster"
BASE_CLASS = "BaseGuildmaster"

MOBILE_HEADER = """# What it is for:
#   The guildmasters of the trades: a vendor-like NPC with the skills of its trade, the main ones from 80 to 100, who teaches them
#   and takes members for the guild of the trade (npc_guild). One man and one woman for each trade; the npc list
#   of the same name, without the sex, picks one of them.
#
# Fields:
#   see templates.md; npc_guild is the guild the NPC takes members for: a player says join to it and pays 500 gold
"""

LIST_HEADER = """# What it is for:
#   One npc list for each trade of guildmaster, with its man and its woman, so that a spawn names the trade only.
#
# Fields:
#   see templates.md
"""

# The skills of this server (``SkillType``), which ModernUO's ``SkillName`` shares by name apart from those below.
SKILL_TYPES = [
    "Alchemy", "Anatomy", "AnimalLore", "ItemIdentification", "ArmsLore", "Parrying", "Begging", "Blacksmithy", "BowcraftFletching",
    "Peacemaking", "Camping", "Carpentry", "Cartography", "Cooking", "DetectingHidden", "Discordance", "EvaluatingIntelligence", "Healing",
    "Fishing", "ForensicEvaluation", "Herding", "Hiding", "Provocation", "Inscription", "Lockpicking", "Magery", "ResistingSpells",
    "Tactics", "Snooping", "Musicianship", "Poisoning", "Archery", "SpiritSpeak", "Stealing", "Tailoring", "AnimalTaming",
    "TasteIdentification", "Tinkering", "Tracking", "Veterinary", "Swordsmanship", "MaceFighting", "Fencing", "Wrestling", "Lumberjacking",
    "Mining", "Meditation", "Stealth", "RemoveTrap", "Necromancy", "Focus", "Chivalry", "Bushido", "Ninjitsu", "Spellweaving", "Mysticism",
    "Imbuing", "Throwing",
]  # fmt: skip

# The skills of ModernUO under the names of this server.
SKILLS = {
    "Blacksmith": "Blacksmithy",
    "DetectHidden": "DetectingHidden",
    "EvalInt": "EvaluatingIntelligence",
    "Forensics": "ForensicEvaluation",
    "Inscribe": "Inscription",
    "ItemID": "ItemIdentification",
    "Macing": "MaceFighting",
    "MagicResist": "ResistingSpells",
    "Parry": "Parrying",
    "Swords": "Swordsmanship",
}

# The graphic ModernUO's classes give the items a guildmaster wears or carries.
GRAPHICS = {
    "FullApron": 0x153D,
    "RingmailChest": 0x13EC,
    "Bascinet": 0x140C,
    "SmithHammer": 0x13E3,
    "Robe": 0x1F03,
    "GnarledStaff": 0x13F8,
    "Kryss": 0x1401,
    "Dagger": 0x0F52,
}

# What covers the torso, which takes the place of the apron or dress.
OVERGARMENTS = {"0x153d_full_apron", "0x1f03_robe", "0x13ec_lbr", "0x13ec_aos", "0x13ec_t2a", "0x13ec_tol"}

# The guild of ModernUO's NpcGuild enum, under the name of this server (``NpcGuildType``).
GUILDS = {
    "MagesGuild": "Mages",
    "WarriorsGuild": "Warriors",
    "ThievesGuild": "Thieves",
    "RangersGuild": "Rangers",
    "HealersGuild": "Healers",
    "MinersGuild": "Miners",
    "MerchantsGuild": "Merchants",
    "TinkersGuild": "Tinkers",
    "TailorsGuild": "Tailors",
    "FishermensGuild": "Fishermen",
    "BardsGuild": "Bards",
    "BlacksmithsGuild": "Blacksmiths",
}

CLOTHES_HUE = "0x0835-0x0852"
HUES = {"Utility.RandomBlueHue()": "0x0515-0x054A", "Utility.RandomYellowHue()": "0x06A5-0x06DA"}


@dataclass
class Equipment:
    items: list[str]
    hue: str | None = None


@dataclass
class Guildmaster:
    cls: str
    title: str
    guild: str | None
    skills: dict[str, str | int]
    outfit: list[Equipment]


@dataclass
class Mobile:
    id: str
    title: str
    gender: str
    guild: str | None
    skills: dict[str, str | int]
    death: int
    equipment: list[Equipment]


# --- reading the C# ---


def to_dice(low: int, high: int) -> str | int:
    """The range as the template writes it: a constant, or ``1dN+M``."""
    low, high = min(low, high), max(low, high)

    if low == high:
        return low

    offset = low - 1
    bonus = "" if offset == 0 else f"+{offset}" if offset > 0 else str(offset)

    return f"1d{high - low + 1}{bonus}"


def read(source: str, path: Path, by_graphic: dict[int, list[str]], report: ConversionReport) -> Guildmaster | None:
    root = csharp.parse(source)
    csharp.check(root, str(path))
    owner = next(csharp.descendants(root, "class_declaration"), None)
    constructors = csharp.members(owner, "constructor_declaration") if owner is not None else []
    initializer = next((child for child in constructors[0].children if child.type == "constructor_initializer"), None) if constructors else None
    arguments = csharp.arguments(initializer) if initializer is not None else []
    title = csharp.string_value(csharp.expression(arguments[0])) if arguments else None

    if owner is None or title is None:
        report.count(f"{path.name} has no constructor with a title")

        return None

    guild = None
    property_ = next((p for p in csharp.members(owner, "property_declaration") if csharp.name_of(p) == "NpcGuild"), None)
    body = next((child for child in property_.children if child.type == "arrow_expression_clause"), None) if property_ is not None else None
    name = csharp.last_name(csharp.named_children(body)[0]) if body is not None else None

    if name in GUILDS:
        guild = GUILDS[name]
    else:
        # ModernUO's miner guildmaster names no guild: it teaches, and takes no members.
        report.count(f"{csharp.name_of(owner)} names no guild: it teaches only")

    skills: dict[str, str | int] = {}

    for call in csharp.descendants(constructors[0], "invocation_expression"):
        target = call.children[0]
        args = csharp.arguments(call)

        if target.type != "identifier" or csharp.text(target) != "SetSkill" or len(args) != 3:
            continue

        skill_name = csharp.last_name(csharp.expression(args[0]))

        if skill_name is None:
            continue

        skill = SKILLS.get(skill_name, skill_name if skill_name in SKILL_TYPES else None)

        if skill is None:
            report.count(f"skill {skill_name} is not one of this server's")

            continue

        low, high = csharp.double_value(csharp.expression(args[1])), csharp.double_value(csharp.expression(args[2]))

        if low is not None and high is not None:
            skills[snake_case(skill)] = to_dice(int(low), int(high))

    return Guildmaster(csharp.name_of(owner), title, guild, skills, _outfit(owner, by_graphic, report))


def _added(statement) -> object | None:
    """The item an if branch adds: the creation of an ``AddItem`` call, alone in the branch."""
    only = statement

    if statement.type == "block":
        inside = csharp.named_children(statement)

        if len(inside) == 1:
            only = inside[0]

    if only.type != "expression_statement":
        return None

    call = csharp.named_children(only)[0]

    if call.type != "invocation_expression" or call.children[0].type != "identifier" or csharp.text(call.children[0]) != "AddItem":
        return None

    arguments = csharp.arguments(call)

    return csharp.expression(arguments[0]) if arguments else None


def _alternatives(statement) -> list:
    if statement.type == "expression_statement":
        call = csharp.named_children(statement)[0]

        if call.type == "invocation_expression" and call.children[0].type == "identifier" and csharp.text(call.children[0]) == "AddItem":
            arguments = csharp.arguments(call)

            return [csharp.expression(arguments[0]) if arguments else None]

    if statement.type == "local_declaration_statement":
        variables = list(csharp.descendants(statement, "variable_declarator"))

        if len(variables) == 1:
            value = csharp.named_children(variables[0])[-1] if any(child.type == "=" for child in variables[0].children) else None

            if value is not None and value.type == "conditional_expression":
                parts = csharp.named_children(value)

                return [parts[1], parts[2]]

    if statement.type == "if_statement":
        consequence = statement.child_by_field_name("consequence")
        alternative = statement.child_by_field_name("alternative")

        if consequence is not None and alternative is not None:
            first, second = _added(consequence), _added(alternative)

            if first is not None and second is not None:
                return [first, second]

    return []


def _outfit(owner, by_graphic: dict[int, list[str]], report: ConversionReport) -> list[Equipment]:
    """What ``InitOutfit`` adds: each ``AddItem``, and the item picked between two by a coin toss. Items are found by the graphic ModernUO gives their class."""
    entries: list[Equipment] = []
    method = next((m for m in csharp.members(owner, "method_declaration") if csharp.name_of(m) == "InitOutfit"), None)
    block = next((child for child in method.children if child.type == "block"), None) if method is not None else None

    for statement in csharp.named_children(block) if block is not None else []:
        created = [node for node in _alternatives(statement) if node is not None and node.type == "object_creation_expression"]

        if not created:
            continue

        items: list[str] = []

        for creation in created:
            kind = csharp.text(csharp.creation_type(creation))
            candidates = by_graphic.get(GRAPHICS.get(kind, -1))
            item = (era_base(candidates) or (candidates[0] if len(candidates) == 1 else None)) if candidates else None

            if item is None:
                report.count(f"no item template for the outfit item {kind}")

                continue

            items.append(item)

        if items:
            entries.append(Equipment(items, _hue_of(created[0])))

    return entries


def _hue_of(creation) -> str | None:
    """A robe in a random blue or yellow hue; nothing else of the outfit has a hue."""
    arguments = csharp.arguments(creation)

    return HUES.get(csharp.text(csharp.expression(arguments[0]))) if arguments else None


# --- building ---


def _clothes(woman: bool, trade: list[Equipment]) -> list[Equipment]:
    """The clothes of the vendors of this server, then what the guildmaster wears and carries for its trade."""
    entries = [
        Equipment(["0x1517_shirt"], CLOTHES_HUE),
        Equipment(
            ["0x1516", "0x152e_short_pants", "0x1537_kilt", "0x1539_long_pants"] if woman else ["0x152e_short_pants", "0x1539_long_pants"],
            CLOTHES_HUE,
        ),
        Equipment(["0x170b_boots", "0x170d_sandals", "0x170f_shoes", "0x1711_thigh_boots"]),
    ]

    if not any(item in OVERGARMENTS for entry in trade for item in entry.items):
        entries.append(
            Equipment(
                ["0x1fa1_tunic", "0x1f01_plain_dress", "0x153b_half_apron", "0x153d_full_apron"] if woman else ["0x1fa1_tunic", "0x153b_half_apron", "0x153d_full_apron"],
                CLOTHES_HUE,
            )
        )

    return entries + trade


def build(master: Guildmaster) -> tuple[Mobile, Mobile, tuple[str, str, str]]:
    trade = snake_case(master.title)
    man = Mobile(f"m_{trade}_guildmaster", f"the {master.title} guildmaster", "male", master.guild, master.skills, 348, _clothes(False, master.outfit))
    woman = Mobile(f"f_{trade}_guildmaster", f"the {master.title} guildmistress", "female", master.guild, master.skills, 337, _clothes(True, master.outfit))

    return man, woman, (trade + "guildmaster", man.id, woman.id)


# --- writing ---


def serialize_mobiles(mobiles: list[Mobile]) -> str:
    lines: list[str] = []

    for position, mobile in enumerate(mobiles):
        if position:
            lines.append("")

        lines += [
            "[[mobile]]",
            f"id = {tomlout.basic(mobile.id)}",
            'base_id = "basevendor"',
            f'name_list = "{mobile.gender}"',
            f"title = {tomlout.basic(mobile.title)}",
            f'gender = "{mobile.gender}"',
            'race = "human"',
        ]

        if mobile.guild is not None:
            lines.append(f"npc_guild = {tomlout.basic(snake_case(mobile.guild))}")

        lines.append("[mobile.skills]")
        lines += [f"{name} = {tomlout.basic(dice) if isinstance(dice, str) else dice}" for name, dice in mobile.skills.items()]
        lines += ["[mobile.sounds]", f"death = {mobile.death}"]

        for index, entry in enumerate(mobile.equipment):
            if index:
                lines.append("")

            lines += ["[[mobile.equipment]]", f"items = {tomlout.strings(entry.items)}"]

            if entry.hue is not None:
                lines.append(f"hue = {tomlout.basic(entry.hue)}")

    return "\n".join(lines) + "\n"


def serialize_lists(lists: list[tuple[str, str, str]]) -> str:
    lines: list[str] = []

    for position, (list_id, man, woman) in enumerate(lists):
        if position:
            lines.append("")

        lines += ["[[npc_list]]", f"id = {tomlout.basic(list_id)}"]

        for index, mobile_id in enumerate((man, woman)):
            if index:
                lines.append("")

            lines += ["[[npc_list.entries]]", "weight = 1", f"mobile_id = {tomlout.basic(mobile_id)}"]

    return "\n".join(lines) + "\n"


# --- the run ---


def run(source: Path, items: Path, mobiles: Path, npc_lists: Path, output: TextIO, error: TextIO) -> int:
    nested = source / "Mobiles" / "Vendors" / "NPC" / "Guildmasters"
    root = nested if nested.is_dir() else source

    if not root.is_dir():
        error.write(f"ModernUO guildmasters folder does not exist: {root}\n")

        return 2

    if not items.is_dir():
        error.write(f"The item templates folder does not exist: {items}\n")

        return 2

    try:
        report = ConversionReport()
        by_graphic = items_by_graphic(items)
        made: list[Mobile] = []
        lists: list[tuple[str, str, str]] = []

        for path in sorted(root.glob(f"*{SUFFIX}.cs"), key=str):
            if path.stem == BASE_CLASS:
                continue

            master = read(csharp.read_source(path), path, by_graphic, report)

            if master is None:
                continue

            man, woman, entry = build(master)
            made += [man, woman]
            lists.append(entry)

        if not made:
            error.write(f"{root}: no guildmaster found.\n")

            return 2

        write_text(mobiles / MOBILES_FILE, MOBILE_HEADER + serialize_mobiles(made))
        write_text(npc_lists / LISTS_FILE, LIST_HEADER + serialize_lists(lists))
        output.write(f"mobiles/{MOBILES_FILE} ({len(made)} templates), npc_lists/{LISTS_FILE} ({len(lists)} lists)\n")
        report.write(output)

        return 0
    except (SourceError, OSError, UnicodeError) as exception:
        error.write(f"Guildmaster conversion failed: {exception}\n")

        return 2
