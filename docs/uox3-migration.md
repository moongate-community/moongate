# Migrate from UOX3

`mg-uoxconv` converts [UOX3](https://github.com/UOX3DevTeam/UOX3) `.dfn` item
definitions and loot lists into Moongate's `ItemTemplate` and `LootTemplate` TOML.
The shapes it writes are described in [Loading TOML templates](templates.md#the-template-shapes);
no loader reads them yet, so the output is content prepared for that loader.

## Run it

From a source checkout:

```sh
dotnet run --project src/Moongate.UoxItemConverter -- \
  --source <file-or-directory> --destination <dir> [--loot-destination <dir>]
```

Docker images after 0.6.0 bundle the same tool at `/app/mg-uoxconv`; see
[UOX3 content conversion](docker.md#uox3-content-conversion) for a `docker run`
example when there is no local .NET SDK.

`--source` is a single `.dfn` file or a directory scanned recursively for every
`.dfn` under it. `--destination` receives one `<name>.toml` per source `.dfn`, at the
same relative path, holding one `[[item]]` per block that has an `id=` of its own.
`--loot-destination` is optional; without it, `LOOTLIST` blocks are skipped. A bare
invocation prints the help and exits `0`; a missing required argument exits `1`.

Every block from every source file is read before any `get=` chain is resolved,
because a chain's target can live in another file: UOX3's own data keeps a sword's
facing variants beside its base definition but a shared `base_item` elsewhere. A
trailing `//comment` is stripped from every line first, as the UOX3 engine does;
real data glues one straight onto a block's opening brace (`{//approximately 1%`).

## What maps

Verified against real UOX3 data:

| UOX3 | ItemTemplate | Note |
| --- | --- | --- |
| The block's own `id=` | `ItemId` | Required; a block with no `id=` is not converted at all |
| The block header, or `name=` when the header is a bare hex | `Id` | Run through `StringUtils.ToSnakeCase`; `name=` is free text ("pitcher of wine") |
| `name=` | `Name` | Carried as-is; UOX3 does not separate an identifier from display text |
| A single-target `get=` | `BaseId` | Only when that target itself converted; `get=a b`, an alias with no `id=` of its own, converts nothing |
| `movable=1` | `Movable` | Anything else, including absent, is `false` |
| `color=` | `Hue` | A fixed value, not a range |
| `weightmax=` | `MaxWeight` | |
| `visible=1`, `2` or `3` | `Visibility = game_master` | Hidden, magically invisible or GM hidden all keep the item from players; `visible=0` or absent leaves it unset, visible to everyone |

Everything else has no home in `ItemTemplate` yet and is dropped: weight, value,
layer, the combat stat fields, `colorlist`, `pileable` (tiledata already carries it),
`script=`, and the multi and geometry fields. `BaseId` is a pointer only: the
converter does not flatten a parent's fields into its children; the loader will
resolve the chain once it exists.

## Loot tables

UOX3's `[LOOTLIST name] { ... }` blocks are weighted loot tables. They convert into
`--loot-destination` (`templates/loots/`, next to `templates/items/`) as one
`<id>.toml` per table, named after the table's own Id rather than the source file:
real UOX3 data defines all 71 tables in one `lootlists.dfn`, and reviewing one has
no reason to load every other table alongside it. Each entry line is:

```text
weight|entry[,amount]
```

`weight` defaults to `1` when the `weight|` prefix is absent. `entry` is an item
header, resolved through the same map `get=` uses; `LOOTLIST=other`, a nested
weighted pick from another table; or the literal `blank`, a real weighted chance of
dropping nothing. `amount` is a single count or `min max` (a space, not a dash) and
maps onto `LootEntry.Amount`, a `RangeValueSpec<int>`.

| UOX3 | LootTemplate / LootEntry | Note |
| --- | --- | --- |
| The block header's name, after `LOOTLIST ` | `LootTemplate.Id` | Also through `StringUtils.ToSnakeCase`; real names are camelCase ("eartheleLoot") |
| An entry's `weight\|` prefix | `LootEntry.Weight` | Defaults to `1` |
| An item header entry | `LootEntry.ItemId` | Also fills `Comment` with that item's own `name=`, when it had one |
| `LOOTLIST=other` | `LootEntry.LootTemplateId` | Only when `other` itself converted |
| `blank` | Neither `ItemId` nor `LootTemplateId` set | A real, weighted chance of nothing |
| A trailing `,amount` | `LootEntry.Amount` | `RangeValueSpec<int>`; `min max` (space) becomes a range |

`ITEMLIST=`, UOX3's "spawn every entry" sibling, is a different mechanic, not a
weighted pick, and never appears in real `lootlists.dfn` data; it is dropped, as is
any entry the converter cannot resolve.

## Verifying the output

After writing every file, the converter reads all of it back from disk, as a real
loader would, and checks it: no two items or loot tables share an Id, and every
`BaseId`, `LootEntry.ItemId` and `LootEntry.LootTemplateId` names something that
was written. This catches a TOML round-trip going wrong and two headers, say
`Base-Item` and `base_item`, that collide only once both go through `ToSnakeCase`.
Any problem exits `1` and lists every one, prefixed `Verification failed:`; a clean
run prints `Verified <N> item(s) and <M> loot table(s) read back from disk`.
