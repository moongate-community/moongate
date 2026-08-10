# World decoration

The static scenery that makes the world look inhabited — lamp posts, fences, signposts, market
stalls, the furniture inside every building. Roughly **63,500 objects** across the five facets,
authored upstream and shipped with the server as data.

Decoration is **not** placed at boot. The catalogue is loaded into memory at startup, and the objects
only become world when someone runs [`decorate`](#the-decorate-command). Writing sixty thousand
persisted items is not something a boot should do behind your back.

## What a decoration object is

An ordinary [item](../scripting/reference/item.md), with a serial, stored in the item store like any
other. It can in principle be picked up, moved and deleted, and it survives a restart because it is
persisted rather than regenerated.

Every one of them points at a single template, `world_decoration`, and carries its own graphic and
hue on the instance. The corpus references **2,381 distinct graphics**, and the alternative — a
template per graphic — would fill the template registry, which drives spawning, loot and the admin
catalogue, with 2,381 entries nobody will ever spawn.

## Where the data lives

Eight YAML files under `Assets/World/Decorations/`, embedded in the server and seeded into
`<root>/data/decorations/` on first boot. Each holds a list of groups: one declaration and every
coordinate it stands at.

```yaml
- Type: Static
  ItemId: 0x0753
  Hue: 0x849
  At:
    - [555, 425, -13]
    - [556, 425, -13]
```

`Type` is what the source called it — `Static` for most of the corpus, otherwise a class name such as
`MetalDoor`. `At` entries are `[x, y, z]` triples, and `z` is signed: dungeon decoration is deeply
negative.

### Files and facets

Most files name their facet. Three do not, and one of them is loaded twice:

| File | Facets |
| --- | --- |
| `britannia.yaml` | Felucca **and** Trammel |
| `felucca.yaml` | Felucca |
| `trammel.yaml` | Trammel |
| `ilshenar.yaml` | Ilshenar |
| `malas.yaml` | Malas |
| `tokuno.yaml` | Tokuno |
| `ruinedmaginciafel.yaml` | Felucca |
| `ruinedmaginciatram.yaml` | Trammel |

Britannia is the shared landmass. Felucca and Trammel are mirrors of the same continent — the towns
stand on both — so its decoration is authored once and loaded onto each. The facet-specific files
hold only what differs between them. The two Magincia files are the post-invasion ruins of each
facet's Magincia, so they stay one map apiece.

## The `decorate` command

Administrator level, from the [admin console](admin-console.md) or in-game:

```text
> decorate
Placing 63528 catalogued decoration object(s). The world pauses while it runs.
Decoration done: placed 62371, skipped 1157 already present.
```

**It stalls the world while it runs** — a few seconds on a cold world, because every object is a
persisted write on the single game-loop thread. That is inherent to placing this much at once, which
is why the command warns before it starts.

### Running it twice is safe

The second run places nothing:

```text
Decoration done: placed 0, skipped 63528 already present.
```

Idempotence is answered by asking the world, not by recording that the job was done: an object
already standing on that tile with that graphic is left alone. That survives a run interrupted
halfway, and it survives a restart — the check reads the spatial index, which is rebuilt from the
item store. A *different* graphic on the same tile is still placed, because statics stack.

This is also why a first run on an empty world reports a non-zero skip count: the upstream corpus
declares some objects twice, and the same check quietly collapses them.

### When it reports an empty catalogue

```text
Nothing to place: the decoration catalogue is empty.
```

The data loader found nothing at boot — check the startup log for
`Loaded N decoration object(s) across 8 facet file(s)`. A zero there points at the YAML in
`<root>/data/decorations/`, not at the command.

## What is not placed

The corpus declares **283 named types** — `Teleporter`, `MetalDoor`, `LibraryBookcase` and the rest.
These are behaviour classes upstream, not graphics. Every one of them is placed as a static with the
right graphic and no behaviour: doors will not open, teleporters will not teleport, containers are
empty. The declared type is carried on each placement so a later pass can find them.

The attributes that behaviour would need — `PointDest`, `Content`, `Facing`, `LabelNumber`, `Light`,
`Spawn`, `Name`, `ContentType`, `Amount` — are **not** consumed. They are preserved in the upstream
source and reported by the conversion script rather than dropped, so no data has been lost.

Signs are a separate corpus in a different format and are not covered here.

## Re-importing the corpus

`scripts/convert-decorations.cs` converts the upstream `.cfg` files to the YAML above. It is
committed and never run by the server; the conversion happens once, offline, and its output is
tracked.

```bash
dotnet run scripts/convert-decorations.cs -- <source-dir> src/Moongate.Server/Assets/World/Decorations
```

It is deterministic — the same input gives byte-identical output, so a re-import diffs cleanly — and
it prints every attribute key it saw and did not consume, with counts.
