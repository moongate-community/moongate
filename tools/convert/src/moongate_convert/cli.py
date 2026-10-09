"""The ``moongate-convert`` command: one subcommand per converter, with the options of the C# ``mgctl convert`` it replaces."""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Callable
from pathlib import Path
from typing import TextIO

from . import chests, locations, signs, spawn, spawns, teleporters

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


# Commands with options of their own: name -> (what it does, adds its options, runs it from the parsed arguments)
_Custom = Callable[[argparse.Namespace, TextIO, TextIO], int]
CUSTOM: dict[str, tuple[str, Callable[[argparse.ArgumentParser], None], _Custom]] = {
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
