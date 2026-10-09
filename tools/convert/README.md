# moongate-convert

Converters of the data of other Ultima Online emulators (ModernUO, UOX3) into the templates of Moongate. They used to be `mgctl convert`
(C#); the Python ones wrote the same files as those, byte for byte, before the C# ones were removed.

```sh
cd tools/convert
uv run moongate-convert --help
uv run moongate-convert modernuo-signs \
  --source <ModernUO>/Distribution/Data/signs.cfg --destination ../../moongate_root/templates/decorations
```

The converters: `modernuo-signs`, `modernuo-teleporters`, `modernuo-locations`, `modernuo-chests`, `modernuo-books`, `modernuo-vendors`,
`modernuo-guildmasters` and `modernuo-spawns`, and of UOX3 `uox` with every pass (items, loot lists, mobiles, name lists, starting items, npc lists
and spawns). The exit codes are: 0 done, 2 a source that is missing or is not what it should be
(nothing is written); `uox` exits 1 when reading what it wrote back finds an id twice or a reference that does not resolve.

## Tests

```sh
uv run pytest                                              # the unit tests
MOONGATE_MODERNUO_DIR=<ModernUO checkout> uv run pytest    # and the golden tests
MOONGATE_UOX3_DIR=<UOX3 checkout> uv run pytest            # and the golden tests of UOX3
```

The golden tests convert the real sources and compare, byte for byte, with the files shipped in `moongate_root`. When ModernUO changes a
file the shipped one is stale: run the converter and ship its output.

`tests/test_golden_uox.py` converts the items and loot lists of UOX3 and compares them with the shipped `templates/items` and `templates/loots`; 24 of the shipped item files were made before the converter read the combat fields, or had a script id added by hand, and the test lists them.

`tests/test_golden_uox_mobiles.py` does it for the mobile pass (`--mobile-source`: the NPCs, the creatures and the name lists): `names.toml` is the shipped one, and the mobile templates are the shipped ones but for the files edited by hand since they were converted (the script id sat after the id then, the shopkeepers are invulnerable, the humans, elementals and undead were tuned); the test names each of those and fails when anything else differs. `tests/test_uox_mobiles.py` ports the C# tests of the pass. Two sources the C# converter dies on are a bad source here: two headers that make one mobile id (exit 2), and a range of numbers too wide for a die (counted as a bad number).

`tests/test_golden_uox_spawns.py` does the same for the starting items, the NPC lists and the spawn regions, against the shipped mobile templates: the shipped spawn files lack the `item_ids = []` line the converter writes, `npclists_towns.toml` lacks the thief guildmaster list, and the common set of `data/starting_items.toml` was edited by hand (the blank book and the welcome letter); the tests name each of these and fail when anything else differs.

`tests/test_e2e.py` is the end to end proof: it copies the shipped `templates` and `data/locations.toml`, runs the installed `moongate-convert` command (a real process) for every converter, in the order an operator would, and requires the copy to come out as the shipped tree, file for file and byte for byte (the thief and ranger shops aside). It needs `MOONGATE_MODERNUO_DIR` too.

CI runs `uv run pytest` (the `convert-tests` job) on every pull request; the golden and end to end tests need the ModernUO and UOX3 checkouts and run locally.

`tests/test_sync_with_the_server.py` reads the C# enums the converters repeat (the maps, the skills, the guilds) and fails when the two drift.

The converters that read ModernUO's C# (`books`, `vendors`, `guildmasters`) do it with tree-sitter (`csharp.py`), as syntax: nothing is
compiled or run. A literal has the value the C# compiler gives it. `modernuo-books` checks a book's id, its limits and its pages, but not that
it fits the packets of the client: that stays with the C# tests of the shipped catalog (`RepositoryTemplateFilesTests`), which load every
book with the server's own renderer. `modernuo-vendors` makes a shop for the thief and the ranger too, since their mobile templates exist
now; the golden test knows they are not shipped.

## Writing a converter

A converter is a module with `run(source, destination, output, error) -> int`, registered in `cli.COMMANDS`. It reads everything before it
writes anything, so a bad source leaves the files of the earlier run alone. What it leaves out is counted in a `ConversionReport` and
printed at the end.
