"""The ``moongate-convert`` command: one subcommand per converter, with the options of the C# ``mgctl convert`` it replaces."""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Callable
from pathlib import Path
from typing import TextIO

from . import books, chests, guildmasters, locations, signs, spawn, spawns, teleporters, vendors

# name -> (module run function, what --source is, what --destination is, help)
_Run = Callable[[Path, Path, TextIO, TextIO], int]
COMMANDS: dict[str, tuple[_Run, str, str, str]] = {
    "modernuo-signs": (
        signs.run,
        "ModernUO's Distribution/Data/signs.cfg file",
        "the decorations folder (templates/decorations); each folder gets a signs.toml, replacing that of a previous run",
        "Convert the signs of ModernUO into decoration files",
    ),
    "modernuo-teleporters": (
        teleporters.run,
        "ModernUO's Distribution/Data/teleporters.json file",
        "the decorations folder (templates/decorations); each map gets a teleporters.toml, replacing that of a previous run",
        "Convert the teleporters of ModernUO into decoration files",
    ),
    "modernuo-locations": (
        locations.run,
        "ModernUO's Distribution/Data/Locations folder",
        "the locations file to write (data/locations.toml), replacing that of a previous run",
        "Convert the named places of ModernUO into the locations data file",
    ),
    "modernuo-books": (
        books.run,
        "the Projects/UOContent folder of ModernUO, or a folder containing static book C# definitions",
        "the book templates folder: one TOML file a book, named after its class; existing generated names are replaced",
        "Convert ModernUO's static BookContent definitions into readable document templates",
    ),
    "modernuo-chests": (
        chests.run,
        "ModernUO's Distribution/Data/Spawns folder",
        "the spawns folder (templates/spawns); each map folder gets a treasure_chests.toml, replacing that of a previous run",
        "Convert the treasure chests of ModernUO's spawners into spawn regions of items",
    ),
}


def _spawns_options(command: argparse.ArgumentParser) -> None:
    command.add_argument("--source", required=True, type=Path, help="ModernUO's Distribution/Data/Spawns folder")
    command.add_argument("--maps", required=True, help="the maps to convert, comma separated, such as malas,tokuno,termur")
    command.add_argument(
        "--mobiles", required=True, type=Path, help="the mobile templates folder (templates/mobiles): spawners naming no template there are skipped"
    )
    command.add_argument(
        "--destination",
        required=True,
        type=Path,
        help="the spawns folder (templates/spawns); each map gets modernuo_*.toml files, replacing those of a previous run",
    )
    command.add_argument(
        "--only",
        default="",
        help="converts only the spawner entries of the classes whose name ends with this, such as Guildmaster, into "
        "modernuo_<name>s.toml, leaving the other modernuo_ files of the maps alone; empty: all",
    )


def _spawns(arguments: argparse.Namespace, output: TextIO, error: TextIO) -> int:
    chosen = []

    for name in (part.strip() for part in arguments.maps.split(",")):
        if not name:
            continue

        known = next((candidate for candidate in spawn.MAP_NAMES if candidate.lower() == name.lower()), None)

        if known is None:
            error.write(f"Unknown map: {name}\n")

            return 2

        chosen.append(known)

    return spawns.run(
        Path(os.path.abspath(arguments.source)),
        chosen,
        Path(os.path.abspath(arguments.mobiles)),
        Path(os.path.abspath(arguments.destination)),
        output,
        error,
        arguments.only or None,
    )


def _vendors_options(command: argparse.ArgumentParser) -> None:
    command.add_argument("--source", required=True, type=Path, help="the Projects/UOContent folder of ModernUO, or its Mobiles/Vendors folder")
    command.add_argument(
        "--items", required=True, type=Path, help="the item templates folder (templates/items): a line becomes the template with the graphic ModernUO gives it"
    )
    command.add_argument("--mobiles", required=True, type=Path, help="the mobile templates folder (templates/mobiles): a vendor class with no template there is skipped")
    command.add_argument(
        "--destination", required=True, type=Path, help="the shops folder (templates/shops); each vendor gets a file named after it, replacing that of a previous run"
    )


def _vendors(arguments: argparse.Namespace, output: TextIO, error: TextIO) -> int:
    return vendors.run(
        Path(os.path.abspath(arguments.source)),
        Path(os.path.abspath(arguments.items)),
        Path(os.path.abspath(arguments.mobiles)),
        Path(os.path.abspath(arguments.destination)),
        output,
        error,
    )


def _guildmasters_options(command: argparse.ArgumentParser) -> None:
    command.add_argument(
        "--source", required=True, type=Path, help="the Projects/UOContent folder of ModernUO, or its Mobiles/Vendors/NPC/Guildmasters folder"
    )
    command.add_argument(
        "--items", required=True, type=Path, help="the item templates folder (templates/items): the items a guildmaster wears and carries are found there"
    )
    command.add_argument(
        "--mobiles", required=True, type=Path, help="the mobile templates folder (templates/mobiles); guildmasters.toml is written there, replacing that of a previous run"
    )
    command.add_argument("--npc-lists", required=True, type=Path, help="the npc lists folder (templates/npc_lists); npclists_guildmasters.toml is written there")


def _guildmasters(arguments: argparse.Namespace, output: TextIO, error: TextIO) -> int:
    return guildmasters.run(
        Path(os.path.abspath(arguments.source)),
        Path(os.path.abspath(arguments.items)),
        Path(os.path.abspath(arguments.mobiles)),
        Path(os.path.abspath(arguments.npc_lists)),
        output,
        error,
    )


# Commands with options of their own: name -> (what it does, adds its options, runs it from the parsed arguments)
_Custom = Callable[[argparse.Namespace, TextIO, TextIO], int]
CUSTOM: dict[str, tuple[str, Callable[[argparse.ArgumentParser], None], _Custom]] = {
    "modernuo-guildmasters": (
        "Convert the guildmasters of ModernUO into mobile templates (a man and a woman for each trade) and npc lists",
        _guildmasters_options,
        _guildmasters,
    ),
    "modernuo-vendors": (
        "Convert the shops of ModernUO's vendors (the SBInfo classes) into shop templates, one file a vendor class",
        _vendors_options,
        _vendors,
    ),
    "modernuo-spawns": (
        "Convert the spawners of ModernUO into spawn regions, for the maps UOX3 has no spawns for",
        _spawns_options,
        _spawns,
    ),
}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="moongate-convert",
        description="Convert the data of other Ultima Online emulators into the templates of Moongate.",
    )
    commands = parser.add_subparsers(dest="command", required=True, metavar="command")

    for name, (_, source_help, destination_help, summary) in COMMANDS.items():
        command = commands.add_parser(name, help=summary, description=summary)
        command.add_argument("--source", required=True, type=Path, help=source_help)
        command.add_argument("--destination", required=True, type=Path, help=destination_help)

    for name, (summary, add_options, _) in CUSTOM.items():
        add_options(commands.add_parser(name, help=summary, description=summary))

    return parser


def main(argv: list[str] | None = None, output: TextIO | None = None, error: TextIO | None = None) -> int:
    output = output or sys.stdout
    error = error or sys.stderr
    arguments = build_parser().parse_args(argv)

    if arguments.command in CUSTOM:
        return CUSTOM[arguments.command][2](arguments, output, error)

    run, *_ = COMMANDS[arguments.command]

    return run(Path(os.path.abspath(arguments.source)), Path(os.path.abspath(arguments.destination)), output, error)


if __name__ == "__main__":
    raise SystemExit(main())
