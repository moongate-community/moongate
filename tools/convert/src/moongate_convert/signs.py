"""Converts ModernUO's shop and world signs (``Distribution/Data/signs.cfg``, placed by ``[SignGen``) into decoration files.

A line is ``<facet> <graphic> <x> <y> <z> <text>``; a text of ``#`` and a number is a cliloc. The signs of each facet are written to
``<folder>/signs.toml`` (``britannia`` for Trammel and Felucca), replacing those of a previous run.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import TextIO

from .textutil import read_lines, toml_escaped, try_int, write_text

FILE_NAME = "signs.toml"

# signs.cfg's facet numbers, in order: 0 is Trammel and Felucca.
FOLDERS = ["britannia", "felucca", "trammel", "ilshenar", "malas", "tokuno"]
MAP_NAMES = ["Trammel and Felucca", "Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno"]


@dataclass
class _Block:
    item_id: int
    text: str
    hue: int
    locations: list[tuple[int, int, int]] = field(default_factory=list)


def run(source: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    if not source.is_file():
        error.write(f"ModernUO signs.cfg does not exist: {source}\n")

        return 2

    blocks: list[list[_Block]] = [[] for _ in FOLDERS]

    for index, line in enumerate(read_lines(source)):
        if not line.strip():
            continue

        parsed = _parse(line)

        if parsed is None:
            error.write(f"{source}: line {index + 1} is not a sign: {line}\n")

            return 2

        facet, item_id, x, y, z, text = parsed
        hue = _hue(facet, x, y)
        facet_blocks = blocks[facet]
        block = next((b for b in facet_blocks if b.item_id == item_id and b.hue == hue and b.text == text), None)

        if block is None:
            block = _Block(item_id, text, hue)
            facet_blocks.append(block)

        block.locations.append((x, y, z))

    for facet, folder in enumerate(FOLDERS):
        path = destination / folder / _file_name_of(facet)

        if not blocks[facet]:
            path.unlink(missing_ok=True)

            continue

        write_text(path, _write(facet, blocks[facet]))
        total = sum(len(block.locations) for block in blocks[facet])
        output.write(f"{folder}/{_file_name_of(facet)}: {total} signs in {len(blocks[facet])} blocks\n")

    return 0


def _parse(line: str) -> tuple[int, int, int, int, int, str] | None:
    parts = line.split(" ", 5)

    if len(parts) != 6:
        return None

    numbers = [try_int(part) for part in parts[:5]]
    text = parts[5]

    if any(number is None for number in numbers) or not 0 <= numbers[0] < len(FOLDERS):  # type: ignore[operator]
        return None

    if not text or (text[0] == "#" and try_int(text[1:]) is None):
        return None

    facet, item_id, x, y, z = numbers  # type: ignore[misc]

    return facet, item_id, x, y, z, text


def _file_name_of(facet: int) -> str:
    """The signs of Trammel alone are those of the old Haven, a ruin on a modern map: their file is set aside with an underscore."""
    return "_" + FILE_NAME if FOLDERS[facet] == "trammel" else FILE_NAME


def _hue(facet: int, x: int, y: int) -> int:
    """ModernUO's SignParser: the signs of Luna and Umbra take the hue of their town."""
    if FOLDERS[facet] != "malas":
        return 0

    if x >= 965 and y >= 502 and x <= 1012 and y <= 537:
        return 0x47E

    if x >= 1960 and y >= 1278 and x < 2106 and y < 1413:
        return 0x44E

    return 0


def _write(facet: int, blocks: list[_Block]) -> str:
    folder = FOLDERS[facet]
    text = (
        "# ==============================================================================\n"
        f"# Moongate - templates/decorations/{folder}/{_file_name_of(facet)}\n"
        "#\n"
        "# What it is for:\n"
        f"#   The shop and world signs placed on {MAP_NAMES[facet]}. Each block is one sign\n"
        "#   placed at every location it lists.\n"
        "#\n"
        "# Fields:\n"
        "#   type       LocalizedSign for a text of the client (label_number), Sign for a\n"
        "#              written name\n"
        "#   item_id    the graphic\n"
        "#   props      label_number, the cliloc of the text, or name; hue when the sign\n"
        "#              has one\n"
        "#   locations  [x, y, z] of every sign of the block\n"
        "# ==============================================================================\n"
    )
    lines = [text]

    for block in blocks:
        localized = block.text[0] == "#"
        props = f"label_number = {block.text[1:]}" if localized else f'name = "{toml_escaped(block.text)}"'

        if block.hue != 0:
            props += f", hue = 0x{block.hue:04X}"

        lines.append("\n[[decoration]]\n")
        lines.append('type = "LocalizedSign"\n' if localized else 'type = "Sign"\n')
        lines.append(f"item_id = 0x{block.item_id:04X}\n")
        lines.append(f"props = {{ {props} }}\n")

        if len(block.locations) == 1:
            x, y, z = block.locations[0]
            lines.append(f"locations = [[{x}, {y}, {z}]]\n")

            continue

        lines.append("locations = [\n")
        lines.extend(f"    [{x}, {y}, {z}],\n" for x, y, z in block.locations)
        lines.append("]\n")

    return "".join(lines)
