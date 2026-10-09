"""``modernuo-taming``: the creatures of ModernUO that can be tamed (``Mobiles/**/*.cs``) into ``data/taming.toml``.

A creature class that sets ``Tamable = true;``, ``MinTameSkill = 29.1;`` and optionally ``ControlSlots = 2;`` in its constructor becomes a
``[[creature]]`` whose template is the lower-cased class name, when the templates of this server have it. Nothing is run: the C# is read as
syntax.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import TextIO

from . import csharp
from .csharp import SourceError
from .report import ConversionReport
from .textutil import write_text

FILE = "taming.toml"

# Templates of this server that are colours of a ModernUO creature: the horse comes in three coats here.
ALIASES = {"brownhorse": "horse", "grayhorse": "horse", "darkhorse": "horse"}
MOBILES = "Mobiles"
MIN_SKILL = -50

HEADER = """# ==============================================================================
# Moongate - taming.toml
#
# What it is for:
#   The creatures a player can tame with the Animal Taming skill, with the skill each asks and the followers it
#   counts for once it is tamed. A creature with no entry here cannot be tamed. Without this file nothing can.
#   Generated from ModernUO by moongate-convert modernuo-taming.
#
# Fields:
#   [[creature]]   one creature that can be tamed
#     template     the id of its mobile template
#     min_skill    the Animal Taming it takes, in points, -50 to 120 (the small animals ask less than none); a try has a
#                  chance from 0.1 under it to 49.9 above
#     slots        how many followers it counts for, 1 to 10
# ==============================================================================
"""

_TEMPLATE_ID = re.compile(r'^id\s*=\s*"([^"]+)"', re.MULTILINE)


@dataclass
class Creature:
    template: str
    min_skill: float
    slots: int


def template_ids(templates: Path) -> set[str]:
    """The ids of the mobile templates of this server."""
    ids: set[str] = set()

    for path in templates.rglob("*.toml"):
        ids.update(_TEMPLATE_ID.findall(path.read_text(encoding="utf-8")))

    return ids


def number(node) -> float | None:
    """A literal number of the C#, an integer or a real one, with its minus sign if it has one."""
    if node.type == "prefix_unary_expression" and csharp.text(node).startswith("-"):
        operand = number(csharp.named_children(node)[-1])

        return None if operand is None else -operand

    value = csharp.double_value(node)

    if value is None:
        integer = csharp.int_value(node)
        value = None if integer is None else float(integer)

    return value


def read(source: str, path: Path, report: ConversionReport) -> list[tuple[str, float | None, int, bool]]:
    """The classes of a file as (name, MinTameSkill, ControlSlots, Tamable) for each that sets ``Tamable``."""
    root = csharp.parse(source)
    csharp.check(root, str(path))
    found: list[tuple[str, float | None, int, bool]] = []

    for cls in csharp.descendants(root, "class_declaration"):
        tamable = False
        skill: float | None = None
        slots = 1
        sets_tamable = False

        for assignment in csharp.descendants(cls, "assignment_expression"):
            owner = next((a for a in csharp.ancestors(assignment) if a.type == "class_declaration"), None)

            if owner is None or owner.id != cls.id:
                continue

            left, right = assignment.child_by_field_name("left"), assignment.child_by_field_name("right")

            if left is None or right is None:
                continue

            name = csharp.text(left)

            if name == "Tamable":
                sets_tamable = True
                tamable = csharp.text(right) == "true"
            elif name == "MinTameSkill":
                skill = number(right)
            elif name == "ControlSlots":
                value = csharp.int_value(right)
                slots = value if value is not None else 1

        if sets_tamable:
            found.append((csharp.name_of(cls), skill, slots, tamable))

    return found


def run(source: Path, templates: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    nested = source / MOBILES
    root = nested if nested.is_dir() else source

    if not root.is_dir():
        error.write(f"ModernUO mobiles folder does not exist: {root}\n")

        return 2

    if not templates.is_dir():
        error.write(f"The mobile templates folder does not exist: {templates}\n")

        return 2

    try:
        report = ConversionReport()
        ids = template_ids(templates)
        creatures: dict[str, Creature] = {}

        for path in sorted((path for path in root.rglob("*.cs") if path.is_file()), key=str):
            for name, skill, slots, tamable in read(csharp.read_source(path), path, report):
                template = name.lower()
                # A creature that does not name its skill asks none, as ModernUO's default.
                skill = 0.0 if skill is None else skill

                if not tamable:
                    report.count("class that sets Tamable to false")
                elif template not in ids:
                    report.count("tamable class with no mobile template of this server")
                elif template in creatures:
                    report.count("tamable class seen twice")
                elif not MIN_SKILL <= skill <= 120 or not 1 <= slots <= 10:
                    report.count("tamable class with a skill or slots out of range")
                else:
                    creatures[template] = Creature(template, skill, slots)

        for alias, original in ALIASES.items():
            if alias in ids and original in creatures and alias not in creatures:
                creatures[alias] = Creature(alias, creatures[original].min_skill, creatures[original].slots)

        if not creatures:
            error.write(f"{root}: no tamable creature found.\n")

            return 2

        write_text(destination / FILE, HEADER + serialize(list(creatures.values())))
        output.write(f"{FILE} ({len(creatures)} creatures)\n")
        report.write(output)

        return 0
    except (SourceError, OSError, UnicodeError) as exception:
        error.write(f"Taming conversion failed: {exception}\n")

        return 2


def serialize(creatures: list[Creature]) -> str:
    blocks = []

    for creature in sorted(creatures, key=lambda item: item.template):
        blocks.append(f'[[creature]]\ntemplate = "{creature.template}"\nmin_skill = {creature.min_skill!r}\nslots = {creature.slots}\n')

    return "\n" + "\n".join(blocks)
