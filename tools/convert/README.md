# moongate-convert

Converters of the data of other Ultima Online emulators (ModernUO, UOX3) into the templates of Moongate. They are moving here from
`mgctl convert` (C#), a part at a time; each is accepted when it writes the same files as the C# one.

```sh
cd tools/convert
uv run moongate-convert --help
uv run moongate-convert modernuo-signs \
  --source <ModernUO>/Distribution/Data/signs.cfg --destination ../../moongate_root/templates/decorations
```

Ported so far: `modernuo-signs`, `modernuo-teleporters`, `modernuo-locations`, `modernuo-chests`. The options are those of the `mgctl convert`
command of the same name, and so are the exit codes: 0 done, 2 a source that is missing or is not what it should be (nothing is written).

## Tests

```sh
uv run pytest                                              # the unit tests
MOONGATE_MODERNUO_DIR=<ModernUO checkout> uv run pytest    # and the golden tests
```

The golden tests convert the real sources and compare, byte for byte, with the files shipped in `moongate_root`. When ModernUO changes a
file the shipped one is stale: run the converter and ship its output.

`tests/test_sync_with_the_server.py` reads the C# enums the converters repeat (the maps) and fails when the two drift.

## Writing a converter

A converter is a module with `run(source, destination, output, error) -> int`, registered in `cli.COMMANDS`. It reads everything before it
writes anything, so a bad source leaves the files of the earlier run alone. What it leaves out is counted in a `ConversionReport` and
printed at the end.
