"""The ``moongate-convert`` command: one subcommand per converter."""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Callable
from pathlib import Path
from typing import TextIO

from . import books, chests, crafts, guildmasters, locations, signs, spawn, spawns, taming, teleporters, uox, vendors

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


def _taming_options(command: argparse.ArgumentParser) -> None:
    command.add_argument("--source", required=True, type=Path, help="the Projects/UOContent folder of ModernUO, or its Mobiles folder")
    command.add_argument(
        "--templates", required=True, type=Path, help="the mobile templates folder (templates/mobiles): a creature class with no template there is skipped"
    )
    command.add_argument("--destination", required=True, type=Path, help="the data folder (data); taming.toml is written there, replacing that of a previous run")


def _taming(arguments: argparse.Namespace, output: TextIO, error: TextIO) -> int:
    return taming.run(
        Path(os.path.abspath(arguments.source)),
        Path(os.path.abspath(arguments.templates)),
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


def _crafts_options(command: argparse.ArgumentParser) -> None:
    command.add_argument("--source", required=True, type=Path, help="UOX3's dfndata/create folder, holding resources.dfn and a .dfn a craft")
    command.add_argument("--items", required=True, type=Path, help="the item templates folder (templates/items): what a recipe makes and takes is found there")
    command.add_argument(
        "--destination", required=True, type=Path, help="the crafts folder (data/crafts): resources.toml and a .toml a craft, replacing those of a previous run"
    )


def _crafts(arguments: argparse.Namespace, output: TextIO, error: TextIO) -> int:
    return crafts.run(arguments.source, arguments.items, arguments.destination, output, error)


def _uox_options(command: argparse.ArgumentParser) -> None:
    command.add_argument("--source", required=True, type=Path, help="a UOX3 .dfn file, or a folder scanned for every .dfn under it")
    command.add_argument("--destination", required=True, type=Path, help="the item templates folder: one .toml per source file, at the same relative path")
    command.add_argument(
        "--loot-destination", type=Path, help="the loot templates folder: one .toml per [LOOTLIST] block; without it the loot lists are skipped"
    )
    command.add_argument(
        "--mobile-source", type=Path, help="UOX3's dfndata folder, holding npc/, colors/, creatures/ and newbie/; goes with --mobile-destination and --names-destination"
    )
    command.add_argument("--mobile-destination", type=Path, help="the mobile templates folder: one .toml per source file")
    command.add_argument("--names-destination", type=Path, help="the file to write the name lists of npc/namelists.dfn to (names.toml)")
    command.add_argument(
        "--starting-items-destination", type=Path, help="the file to write the starting items of newbie/newbie.dfn to (starting_items.toml); needs --mobile-source"
    )
    command.add_argument(
        "--scripts-source",
        type=Path,
        help="UOX3's js folder, holding jse_fileassociations.scp and jse_objectassociations.scp: an item whose UOX3 script has a Moongate script gets its script_id",
    )
    command.add_argument(
        "--npc-lists-destination", type=Path, help="the npc lists folder (templates/npc_lists) for UOX3's [NPCLIST] blocks; needs --mobile-source and --spawns-destination"
    )
    command.add_argument(
        "--spawns-destination", type=Path, help="the spawns folder (templates/spawns), one folder a map, for UOX3's [REGIONSPAWN] blocks; needs --mobile-source and --npc-lists-destination"
    )


def _uox(arguments: argparse.Namespace, output: TextIO, error: TextIO) -> int:
    return uox.run(
        arguments.source,
        arguments.destination,
        arguments.loot_destination,
        output,
        error,
        arguments.mobile_source,
        arguments.mobile_destination,
        arguments.names_destination,
        arguments.starting_items_destination,
        arguments.scripts_source,
        arguments.npc_lists_destination,
        arguments.spawns_destination,
    )


# Commands with options of their own: name -> (what it does, adds its options, runs it from the parsed arguments)
_Custom = Callable[[argparse.Namespace, TextIO, TextIO], int]
CUSTOM: dict[str, tuple[str, Callable[[argparse.ArgumentParser], None], _Custom]] = {
    "uox": (
        "Convert UOX3's .dfn item blocks and loot lists into item and loot templates, and with --mobile-source its NPCs, names, starting items, npc lists and spawns",
        _uox_options,
        _uox,
    ),
    "uox-crafts": (
        "Convert UOX3's create menus into the crafts of data/crafts and their resource lists",
        _crafts_options,
        _crafts,
    ),
    "modernuo-guildmasters": (
        "Convert the guildmasters of ModernUO into mobile templates (a man and a woman for each trade) and npc lists",
        _guildmasters_options,
        _guildmasters,
    ),
    "modernuo-taming": (
        "Convert the creatures of ModernUO that can be tamed (Tamable, MinTameSkill, ControlSlots) into data/taming.toml",
        _taming_options,
        _taming,
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
