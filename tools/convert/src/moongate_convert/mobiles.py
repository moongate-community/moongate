"""UOX3 NPC blocks as mobile templates (``MobileTemplate`` of the server): what a template holds, how a block becomes one, how a male and a
female half join into one, and how the file is written.

A block keeps its inheritance as a ``base_id``: every npc block is converted, so a ``get=`` target always exists as a template of its own.
"""

from __future__ import annotations

import copy
import re
from dataclasses import dataclass, field, fields
from pathlib import Path

from . import dfn, item_index, items as items_module, tomlout
from .dfn import DfnBlock, IgnoreCaseDict, IgnoreCaseSet
from .item_index import ItemIndex
from .names import list_id as name_list_id
from .report import ConversionReport
from .specs import DiceSpec, HueSpec
from .textutil import read_lines, snake_case, trim, try_int
from .uox_data import map_skill

# Humanoid bodies carry the race and the gender; the race then gives the body back.
_HUMANOID_BODIES = {
    0x190: ("human", "male"),
    0x191: ("human", "female"),
    0x25D: ("elf", "male"),
    0x25E: ("elf", "female"),
    0x29A: ("gargoyle", "male"),
    0x29B: ("gargoyle", "female"),
}

# Hair and beard item lists: the race gives hair and beard instead.
_HAIR_ITEM_LISTS = {13, 14, 15}

# UOX3's NPCAI values Moongate has a mobile script for: the town guards (4, scripts/mobiles/guard.lua), the bankers (8,
# scripts/mobiles/banker.lua), the creatures that go for everyone (2 evil, 11 evil caster and 88 chaotic: scripts/mobiles/monster.lua, the
# casters fight in melee until there is magic; the fighter, 5, and the caster, 10, are the good ones, who fight criminals only, and have none
# yet), the animals that keep to themselves (6, scripts/mobiles/animal.lua) and those that run (12 scared animal,
# scripts/mobiles/scared_animal.lua).
_AI_SCRIPTS = {
    "2": "monster",
    "4": "guard",
    "6": "animal",
    "8": "banker",
    "11": "monster",
    "12": "scared_animal",
    "88": "monster",
}

# The mobiles a Moongate mobile script is written for, by their UOX3 name; those based on them take it through base_id. The undead of the
# graveyards take the melee AI, the casters among them (wraith, spectre, lich) too until magic exists.
_SCRIPT_IDS = IgnoreCaseDict(
    {
        "skeleton": "monster",
        "zombie": "monster",
        "ghoul": "monster",
        "headless": "monster",
        "wraith": "monster",
        "spectre": "monster",
        "lich": "monster",
        # The creatures the spells of Magery summon: they fight for their master through the pet orders.
        "airele-summon": "monster",
        "earthele-summon": "monster",
        "firele-summon": "monster",
        "waterele-summon": "monster",
        "daemon-summon": "monster",
    }
)

_RACES = {"0": "human", "1": "elf", "2": "gargoyle"}
_NOTORIETY = {"INNOCENT": "innocent", "NEUTRAL": "attackable", "EVIL": "murderer"}


@dataclass
class Sounds:
    """The sounds of a mobile (``MobileSounds``), each by its sound number."""

    start_attack: int | None = None
    idle: int | None = None
    attack: int | None = None
    hurt: int | None = None
    death: int | None = None


@dataclass
class Resistances:
    """Resistances in percent (``MobileResistances``)."""

    physical: DiceSpec | None = None
    fire: DiceSpec | None = None
    cold: DiceSpec | None = None
    poison: DiceSpec | None = None
    energy: DiceSpec | None = None


@dataclass
class EquipmentEntry:
    """One thing a mobile wears or holds (``MobileEquipmentEntry``): any of the items, picked at random, in a hue, for one gender or both."""

    items: list[str] = field(default_factory=list)
    hue: HueSpec | None = None
    gender: str | None = None


@dataclass
class Mobile:
    """A mobile template (``MobileTemplate``); the fields are in the order the server's serializer writes them."""

    id: str
    base_id: str | None = None
    comment: str | None = None
    name: str | None = None
    name_list: str | None = None
    title: str | None = None
    body: int | None = None
    gender: str | None = None
    race: str | None = None
    skin_hue: HueSpec | None = None
    hair: list[int] | None = None
    hair_hue: HueSpec | None = None
    beard: list[int] | None = None
    beard_hue: HueSpec | None = None
    strength: DiceSpec | None = None
    dexterity: DiceSpec | None = None
    intelligence: DiceSpec | None = None
    hits: DiceSpec | None = None
    mana: DiceSpec | None = None
    stamina: DiceSpec | None = None
    damage: DiceSpec | None = None
    armor: DiceSpec | None = None
    resistances: Resistances | None = None
    skills: dict[str, DiceSpec] | None = None
    notoriety: str | None = None
    karma: DiceSpec | None = None
    fame: DiceSpec | None = None
    equipment: list[EquipmentEntry] | None = None
    loot: list[str] | None = None
    gold: DiceSpec | None = None
    sounds: Sounds | None = None
    script_id: str | None = None
    npc_guild: str | None = None
    flee_at: int | None = None
    control_slots: int | None = None
    blood_hue: int | None = None
    visibility: str | None = None
    movement: str | None = None
    tags: dict[str, str] | None = None


FIELD_NAMES = [item.name for item in fields(Mobile)]


@dataclass
class BuildContext:
    """Everything a block is resolved against (``MobileBuildContext``): the dictionary texts, the item pass's ids, the converted npc headers,
    the colour lists, the sounds and movements of each body, the headers whose body swims, and where dropped values are counted."""

    dictionary: dict[int, str]
    items: ItemIndex
    mobile_headers: IgnoreCaseSet
    color_lists: dict[int, HueSpec | None]
    creature_sounds: dict[int, Sounds]
    creature_movements: dict[int, str]
    swimming_headers: IgnoreCaseSet
    report: ConversionReport


# --- creatures.dfn ---

_CREATURE_PREFIX = "CREATURE "


def load_creature_sounds(path: Path) -> dict[int, Sounds]:
    """The sounds of each body from ``creatures.dfn`` (``[CREATURE 0x11]`` blocks); none for a missing file."""
    sounds: dict[int, Sounds] = {}

    if not path.is_file():
        return sounds

    for block in dfn.parse(read_lines(path)):
        body = _creature_body(block)

        if body is None:
            continue

        creature = Sounds(
            start_attack=_sound(block, "SOUND_STARTATTACK"),
            idle=_sound(block, "SOUND_IDLE"),
            attack=_sound(block, "SOUND_ATTACK"),
            hurt=_sound(block, "SOUND_DEFEND"),
            death=_sound(block, "SOUND_DIE"),
        )

        if any(value is not None for value in (creature.start_attack, creature.idle, creature.attack, creature.hurt, creature.death)):
            sounds[body] = creature

    return sounds


def load_creature_movements(path: Path) -> dict[int, str]:
    """Where each body moves, from ``creatures.dfn``: ``MOVEMENT=WATER`` or ``MOVEMENT=BOTH``; a body on land only, the default, is left out."""
    movements: dict[int, str] = {}

    if not path.is_file():
        return movements

    for block in dfn.parse(read_lines(path)):
        body = _creature_body(block)
        movement = block.fields.get("MOVEMENT")

        if body is None or movement is None:
            continue

        match upper_invariant(trim(movement)):
            case "WATER":
                movements[body] = "water"
            case "BOTH":
                movements[body] = "both"

    return movements


def _creature_body(block: DfnBlock) -> int | None:
    if block.header[: len(_CREATURE_PREFIX)].lower() != _CREATURE_PREFIX.lower():
        return None

    return dfn.uox_number(block.header[len(_CREATURE_PREFIX) :])


def _sound(block: DfnBlock, key: str) -> int | None:
    text = block.fields.get(key)

    return None if text is None else dfn.uox_number(text)


def upper_invariant(text: str) -> str:
    """As .NET's ``ToUpperInvariant``: a character whose upper case is several characters (``ß``) stays as it is."""
    return "".join(character.upper() if len(character.upper()) == 1 else character for character in text)


# --- a block becomes a template ---


def is_special_section(header: str) -> bool:
    """Whether a header is a section that is not an npc, such as a name list."""
    return header[:10].lower() == "randomname" or header[:7].lower() == "npclist"


def build(block: DfnBlock, context: BuildContext) -> Mobile | None:
    """The template of a block; None for a section that is not an npc or a block whose ``get=`` names two targets (a gender pair, merged
    separately)."""
    if is_special_section(block.header) or len(dfn.get_targets(block)) > 1:
        return None

    template = Mobile(id=snake_case(block.header))
    _apply_inheritance(block, template, context)
    _apply_script(block, template)
    _apply_identity(block, template, context)
    _apply_numbers(block, template, context)
    _apply_equipment_and_loot(block, template, context)

    return template


def _apply_inheritance(block: DfnBlock, template: Mobile, context: BuildContext) -> None:
    parents = dfn.parent_targets(block)

    if len(parents) != 1:
        return

    if parents[0] in context.mobile_headers:
        template.base_id = snake_case(parents[0])
    else:
        context.report.count("unresolved get")


def _apply_script(block: DfnBlock, template: Mobile) -> None:
    if block.header in _SCRIPT_IDS:
        template.script_id = _SCRIPT_IDS[block.header]
    elif (ai := block.fields.get("NPCAI")) is not None and trim(ai) in _AI_SCRIPTS:
        template.script_id = _AI_SCRIPTS[trim(ai)]


def _apply_identity(block: DfnBlock, template: Mobile, context: BuildContext) -> None:
    id_text = block.fields.get("ID")
    body = None if id_text is None else dfn.uox_number(id_text)

    if body is not None:
        # The block that sets the body carries its sounds; templates inheriting from it get them through base_id.
        if body in context.creature_sounds:
            template.sounds = copy.copy(context.creature_sounds[body])

        parents = dfn.parent_targets(block)

        if body in context.creature_movements:
            template.movement = context.creature_movements[body]
        elif len(parents) == 1 and parents[0] in context.swimming_headers:
            # A land body of its own: a water or amphibious base must not pass its movement on.
            template.movement = "land"

        if body in _HUMANOID_BODIES:
            template.race, template.gender = _HUMANOID_BODIES[body]
        else:
            template.body = body

    # RACE= only means something on a human-shaped body: UOX3's [giantrat] has RACE=2 on a rat.
    if template.race is None and template.body is None and (race_text := block.fields.get("RACE")) is not None:
        template.race = _RACES.get(trim(race_text))

    template.name = _resolve_text(block, "NAME", context)
    template.title = _resolve_text(block, "TITLE", context)

    if (list_text := block.fields.get("NAMELIST")) is not None and (number := try_int(list_text)) is not None:
        template.name_list = name_list_id(number)

    # UOX3 data has f_ NPCs with the male body ([f_scribe] ID=0x0190) and m_ ones with the female body.
    header = block.header.lower()
    prefix_gender = "female" if header.startswith("f_") else "male" if header.startswith("m_") else None

    if template.race is not None and prefix_gender is not None and template.gender is not None and template.gender != prefix_gender:
        template.gender = prefix_gender
        context.report.count("f_ or m_ npc with the other gender's body, gender follows the prefix")

    # And female NPCs using the male name list, or the other way round ([f_paladin] NAMELIST=1).
    if template.gender == "female" and template.name_list == "male":
        template.name_list = "female"
    elif template.gender == "male" and template.name_list == "female":
        template.name_list = "male"


def _resolve_text(block: DfnBlock, key: str, context: BuildContext) -> str | None:
    """A number is a dictionary id; ``#`` means "the comment is the text"; anything else is the text itself."""
    value = block.fields.get(key)

    if value is None or not value:
        return None

    comment = block.comments.get(key)
    text_id = try_int(value)

    if text_id is not None:
        found = context.dictionary.get(text_id)

        return comment if found is None else found

    return comment if value == "#" else value


def to_dice(text: str, divisor: int = 1, maximum: int = 2**31 - 1) -> DiceSpec | None:
    """UOX3's ``lo hi`` or single value as dice: one value is a constant, two are one die spanning them (``96 120`` is ``1d25+95``). Each
    value is divided by ``divisor`` (skills are in tenths) and clamped to ``maximum``. None when the text is not numbers."""
    parts = [part for part in (trim(piece) for piece in text.split(" ")) if part]

    if not 1 <= len(parts) <= 2 or any(dfn.uox_number(part) is None for part in parts):
        return None

    values = [min(_truncating_division(_number(part), divisor), maximum) for part in parts]
    low, high = min(values[0], values[-1]), max(values[0], values[-1])

    if low == high:
        return DiceSpec.from_value(low)

    offset = low - 1
    bonus = "" if offset == 0 else f"+{offset}" if offset > 0 else str(offset)

    return DiceSpec.try_parse(f"1d{high - low + 1}{bonus}")


def _truncating_division(dividend: int, divisor: int) -> int:
    """C#'s integer division, which truncates toward zero."""
    quotient = abs(dividend) // abs(divisor)

    return quotient if (dividend < 0) == (divisor < 0) else -quotient


def _number(text: str) -> int:
    return dfn.uox_number(text) or 0


def _dice(value: str, context: BuildContext, divisor: int = 1, maximum: int = 2**31 - 1) -> DiceSpec | None:
    dice = to_dice(value, divisor, maximum)

    if dice is None:
        context.report.count("bad number")

    return dice


def _entries(block: DfnBlock):
    """The ``key=value`` lines of a block, in order, with the key in upper case."""
    for entry in block.entries:
        separator = entry.find("=")

        if separator >= 0:
            yield upper_invariant(trim(entry[:separator])), trim(entry[separator + 1 :])


def _apply_numbers(block: DfnBlock, template: Mobile, context: BuildContext) -> None:
    # UOX3 applies tags in file order, so a later line wins; HPMAX beats HP wherever it is.
    for key, value in _entries(block):
        if key in ("STR", "ST", "STRENGTH"):
            template.strength = _dice(value, context)
        elif key in ("DEX", "DX", "DEXTERITY"):
            template.dexterity = _dice(value, context)
        elif key in ("INT", "IN", "INTELLIGENCE"):
            template.intelligence = _dice(value, context)
        elif key == "HPMAX" or (key == "HP" and "HPMAX" not in block.fields):
            template.hits = _dice(value, context)
        elif key == "MANAMAX" or (key == "MANA" and "MANAMAX" not in block.fields):
            template.mana = _dice(value, context)
        elif key == "STAMINAMAX" or (key == "STAMINA" and "STAMINAMAX" not in block.fields):
            template.stamina = _dice(value, context)
        elif key in ("DAMAGE", "ATT"):
            template.damage = _dice(value, context)
        elif key == "DEF":
            template.armor = _dice(value, context)
        elif key in ("RESISTFIRE", "RESISTCOLD", "RESISTPOISON", "RESISTLIGHTNING"):
            attribute = {"RESISTFIRE": "fire", "RESISTCOLD": "cold", "RESISTPOISON": "poison", "RESISTLIGHTNING": "energy"}[key]
            template.resistances = template.resistances or Resistances()
            setattr(template.resistances, attribute, _dice(value, context))
        elif key == "ELEMENTRESIST":
            _apply_element_resist(value, template, context)
        elif key == "KARMA":
            template.karma = _dice(value, context)
        elif key == "FAME":
            template.fame = _dice(value, context)
        elif key == "GOLD":
            template.gold = _dice(value, context)
        elif key == "FLEEAT":
            # From 1 to 100, or -1 for never; 0 is UOX3's "the server's default" and a value out of it is left out.
            flee_at = dfn.uox_number(value)

            if flee_at is not None and (flee_at == -1 or 1 <= flee_at <= 100):
                template.flee_at = flee_at
        elif key == "CONTROLSLOTS" and block.header.lower().endswith("-summon"):
            # The creatures the spells of Magery summon count for these followers (1 to 10, else left out); the tamable
            # ones take their slots from data/taming.toml.
            slots = dfn.uox_number(value)

            if slots is not None and 1 <= slots <= 10:
                template.control_slots = slots
        elif key == "FLAG":
            template.notoriety = _NOTORIETY.get(upper_invariant(value), template.notoriety)
        elif key in ("CUSTOMINTTAG", "CUSTOMSTRINGTAG"):
            _apply_tag(value, template)
        elif (skill := map_skill(key)) is not None:
            points = _dice(value, context, 10, 120)

            if points is not None:
                template.skills = template.skills if template.skills is not None else {}
                template.skills[snake_case(skill)] = points


def _apply_element_resist(value: str, template: Mobile, context: BuildContext) -> None:
    """``ELEMENTRESIST=heat cold lightning poison``."""
    parts = [part for part in value.split(" ") if part]

    if len(parts) != 4 or any(dfn.uox_number(part) is None for part in parts):
        context.report.count("bad number")

        return

    resistances = template.resistances = template.resistances or Resistances()
    resistances.fire = DiceSpec.from_value(_number(parts[0]))
    resistances.cold = DiceSpec.from_value(_number(parts[1]))
    resistances.energy = DiceSpec.from_value(_number(parts[2]))
    resistances.poison = DiceSpec.from_value(_number(parts[3]))


def _apply_tag(value: str, template: Mobile) -> None:
    parts = [trim(part) for part in value.split(" ", 1)]

    if len(parts) == 2 and parts[0]:
        template.tags = template.tags if template.tags is not None else {}
        template.tags[parts[0]] = parts[1]


def _apply_equipment_and_loot(block: DfnBlock, template: Mobile, context: BuildContext) -> None:
    """EQUIPITEM opens an entry; COLOR, COLOUR and COLORLIST after it colour that entry, as UOX3 colours the last item it created."""
    last: EquipmentEntry | None = None

    for key, value in _entries(block):
        if key == "EQUIPITEM":
            last = _build_equipment(value, context)

            if last is not None:
                template.equipment = template.equipment if template.equipment is not None else []
                template.equipment.append(last)
        elif key in ("COLOR", "COLOUR") and last is not None:
            hue = HueSpec.try_parse(value)

            if hue is not None:
                last.hue = hue
        elif key in ("COLORLIST", "COLOURLIST") and last is not None:
            entry = last
            _apply_color_list(value, context, lambda hue, entry=entry: setattr(entry, "hue", hue))
        elif key == "SKIN" and template.race is None:
            hue = HueSpec.try_parse(value)

            if hue is not None:
                template.skin_hue = hue
        elif key == "SKINLIST" and template.race is None:
            _apply_color_list(value, context, lambda hue: setattr(template, "skin_hue", hue))
        elif key == "LOOT":
            _apply_loot(value, template, context)


def _build_equipment(value: str, context: BuildContext) -> EquipmentEntry | None:
    list_number = item_index.list_number(value)

    if list_number is not None and list_number in _HAIR_ITEM_LISTS:
        return None

    found = item_index.resolve(value, context.items, context.report)

    return EquipmentEntry(items=found) if found else None


def _apply_color_list(value: str, context: BuildContext, apply) -> None:
    number = try_int(value)

    if number is None or number not in context.color_lists:
        context.report.count("unresolved colour list")

        return

    hue = context.color_lists[number]

    if hue is None:
        context.report.count("colour list not a range")

        return

    apply(hue)


def _apply_loot(value: str, template: Mobile, context: BuildContext) -> None:
    """``LOOT=name``, ``LOOT=name,count`` or ``LOOT=name,min max``: the loot table, count times."""
    parts = [trim(part) for part in value.split(",", 1)]
    loot_id = items_module.try_get_loot_id("LOOTLIST " + parts[0])

    if loot_id is None or loot_id not in context.items.loot_ids:
        context.report.count("unresolved loot")

        return

    times = try_int(parts[1].split(" ")[0]) if len(parts) == 2 else None
    count = times if times is not None and times > 0 else 1

    for _ in range(count):
        template.loot = template.loot if template.loot is not None else []
        template.loot.append(loot_id)


# --- a male and a female half join ---

_MERGED_FIELDS = {"id", "base_id", "gender", "name_list", "equipment", "sounds"}


def try_merge(id_: str, first: Mobile, second: Mobile, resolve, report: ConversionReport) -> Mobile | None:
    """One template with ``gender = "random"`` from a ``GET=m_x f_x`` pair: what both set alike is kept, equipment only one of them wears is
    filtered by gender, and any other difference takes the male value and is reported. None when the two are not a male and a female of one
    race."""
    first_race, first_gender = resolve(first)
    second_race, second_gender = resolve(second)

    if first_race is None or first_race != second_race or (first_gender, second_gender) not in (("male", "female"), ("female", "male")):
        return None

    male, female = (first, second) if first_gender == "male" else (second, first)
    merged = copy.deepcopy(male)
    merged.id = id_
    merged.gender = "random"
    merged.race = first_race

    if male.base_id != female.base_id:
        report.count("base differs between the male and female")

    if male.name_list == "male" and female.name_list == "female":
        merged.name_list = "{gender}"
    elif male.name_list == female.name_list:
        merged.name_list = male.name_list
    else:
        report.count("field differs between the male and female: name_list")
        merged.name_list = male.name_list

    for name in FIELD_NAMES:
        if name not in _MERGED_FIELDS and _describe(name, male) != _describe(name, female):
            report.count(f"field differs between the male and female: {name}")

    # A male sound on a female mobile is wrong, not just imprecise (humans die with gendered screams): when the two differ, neither is kept.
    if _describe("sounds", male) != _describe("sounds", female):
        merged.sounds = None
        report.count("sounds differ between the male and female, left unset")

    merged.equipment = _merge_equipment(male.equipment or [], female.equipment or [])

    return merged


def _merge_equipment(male: list[EquipmentEntry], female: list[EquipmentEntry]) -> list[EquipmentEntry] | None:
    """Shared entries first, unfiltered; then the male-only and the female-only ones, filtered."""
    female_keys = [_equipment_key(entry) for entry in female]
    male_keys = [_equipment_key(entry) for entry in male]
    merged = [EquipmentEntry(entry.items, entry.hue) for entry in male if _equipment_key(entry) in female_keys]
    merged += [EquipmentEntry(entry.items, entry.hue, "male") for entry in male if _equipment_key(entry) not in female_keys]
    merged += [EquipmentEntry(entry.items, entry.hue, "female") for entry in female if _equipment_key(entry) not in male_keys]

    return merged or None


def _equipment_key(entry: EquipmentEntry) -> tuple:
    return tuple(entry.items), None if entry.hue is None else entry.hue.to_toml()


def _describe(name: str, template: Mobile) -> str:
    """The TOML a template with only this field set would have: an exact comparison of a field of any type."""
    probe = Mobile(id="probe")
    setattr(probe, name, getattr(template, name))

    return serialize([probe])


# --- TOML ---

_BARE_KEY = re.compile(r"[A-Za-z0-9_-]+")


def _key(name: str) -> str:
    return name if _BARE_KEY.fullmatch(name) else tomlout.basic(name)


def _scalar(value: object) -> str:
    if isinstance(value, (DiceSpec, HueSpec)):
        return value.to_toml()

    if isinstance(value, str):
        return tomlout.basic(value)

    return str(value)


_PLAIN_FIELDS = [
    "id", "base_id", "comment", "name", "name_list", "title", "body", "gender", "race", "skin_hue", "hair", "hair_hue", "beard",
    "beard_hue", "strength", "dexterity", "intelligence", "hits", "mana", "stamina", "damage", "armor", "notoriety", "karma", "fame",
    "loot", "gold", "script_id", "npc_guild", "flee_at", "control_slots", "blood_hue", "visibility", "movement",
]  # fmt: skip


def serialize(mobiles: list[Mobile]) -> str:
    """The ``[[mobile]]`` tables of a file: the plain fields in the order of the template, then its tables, then the equipment."""
    blocks: list[str] = []

    for mobile in mobiles:
        lines = ["[[mobile]]"]

        for name in _PLAIN_FIELDS:
            value = getattr(mobile, name)

            if value is None:
                continue

            if isinstance(value, list):
                value = "[" + ", ".join(_scalar(item) for item in value) + "]"
                lines.append(f"{name} = {value}")
            else:
                lines.append(f"{name} = {_scalar(value)}")

        if mobile.resistances is not None:
            lines.append("[mobile.resistances]")
            lines += [
                f"{name} = {value.to_toml()}"
                for name in ("physical", "fire", "cold", "poison", "energy")
                if (value := getattr(mobile.resistances, name)) is not None
            ]

        if mobile.skills is not None:
            lines.append("[mobile.skills]")
            lines += [f"{_key(name)} = {value.to_toml()}" for name, value in mobile.skills.items()]

        if mobile.sounds is not None:
            lines.append("[mobile.sounds]")
            lines += [
                f"{name} = {value}"
                for name in ("start_attack", "idle", "attack", "hurt", "death")
                if (value := getattr(mobile.sounds, name)) is not None
            ]

        if mobile.tags is not None:
            lines.append("[mobile.tags]")
            lines += [f"{_key(name)} = {tomlout.basic(value)}" for name, value in mobile.tags.items()]

        text = "\n".join(lines) + "\n"

        for index, entry in enumerate(mobile.equipment or []):
            entry_lines = ["[[mobile.equipment]]", f"items = {tomlout.strings(entry.items)}"]

            if entry.hue is not None:
                entry_lines.append(f"hue = {entry.hue.to_toml()}")

            if entry.gender is not None:
                entry_lines.append(f"gender = {tomlout.basic(entry.gender)}")

            # Each element of an array of tables after the first one has a blank line before it.
            text += ("\n" if index > 0 else "") + "\n".join(entry_lines) + "\n"

        blocks.append(text)

    return "\n".join(blocks)
