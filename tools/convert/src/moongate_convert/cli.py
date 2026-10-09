"""The ``moongate-convert`` command: one subcommand per converter, with the options of the C# ``mgctl convert`` it replaces."""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Callable
from pathlib import Path
from typing import TextIO

from . import chests, locations, signs, teleporters

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

    return parser


def main(argv: list[str] | None = None, output: TextIO | None = None, error: TextIO | None = None) -> int:
    output = output or sys.stdout
    error = error or sys.stderr
    arguments = build_parser().parse_args(argv)
    run, *_ = COMMANDS[arguments.command]

    return run(Path(os.path.abspath(arguments.source)), Path(os.path.abspath(arguments.destination)), output, error)


if __name__ == "__main__":
    raise SystemExit(main())
