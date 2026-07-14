# Ultima client assets

Moongate requires an Ultima client-data directory because maps, tiles, artwork, interface graphics, animations, localization, fonts, audio, and multis are decoded from the installed client files. `FilesLoaderService` establishes that directory at startup; `Moongate.Ultima` contains the readers and rendering helpers, while `Moongate.UO.Data` contains the server's typed data records and enums.

## File access and asset groups

`Files` maps the known client filenames case-insensitively, which matters on case-sensitive hosts. `FileIndex` selects a UOP accessor when a located path ends in `.uop`, otherwise a paired MUL/index accessor, and applies visible `verdata` patches. Missing paths leave the index without an accessor, allowing higher-level readers to report missing assets.

The readers divide into these subsystem concerns:

- Maps and tiles: `Map`, `TileMatrix`, `TileMatrixPatch`, `TileData`, and radar-color support decode land, statics, tile metadata, and map patches.
- Art and interface graphics: `Art`, `Gumps`, `Texmaps`, `Textures`, `Light`, and hue readers decode the principal bitmap families.
- Animations: `Animations`, body conversion/table data, UOP animation loading, and animation frames resolve body/action/direction sequences.
- Localization, fonts, and audio: string/cliloc and speech lists, ASCII and Unicode fonts, and `Sounds` cover text and sound resources.
- Multis: `Multis` and `MultiComponentList` assemble multi-tile structures from legacy or collection data.
- Caching: bounded LRU caches retain decoded bitmaps and animation-frame arrays; capacity is configured through `Files`. Cache ownership and disposal rules are explicit in the cache implementations.
- Rendering helpers: `ItemCatalog` returns encoded item images and metadata, `BodyRenderer` encodes animation frames as PNG, and `PaperdollComposer` combines gumps, hues, tile metadata, and equipment draw order into a composed image.

These classes form a decoding and presentation layer over client assets. They are not the editable YAML data sources loaded into server registries, and this page does not imply that every reader is initialized eagerly at server startup.

## Source map

### Runtime

- `src/Moongate.Server/Services/Loading/FilesLoaderService.cs`
- `src/Moongate.Ultima/Io/Files.cs`
- `src/Moongate.Ultima/Io/FileIndex.cs`
- `src/Moongate.Ultima/Io/MulFileAccessor.cs`
- `src/Moongate.Ultima/Io/UopFileAccessor.cs`
- `src/Moongate.Ultima/Io/Verdata.cs`
- `src/Moongate.Ultima/Maps/Map.cs`
- `src/Moongate.Ultima/Maps/TileMatrix.cs`
- `src/Moongate.Ultima/Tiles/TileData.cs`
- `src/Moongate.Ultima/Graphics/Art.cs`
- `src/Moongate.Ultima/Graphics/Gumps.cs`
- `src/Moongate.Ultima/Graphics/Texmaps.cs`
- `src/Moongate.Ultima/Animation/Animations.cs`
- `src/Moongate.Ultima/Localization/StringList.cs`
- `src/Moongate.Ultima/Fonts/UnicodeFonts.cs`
- `src/Moongate.Ultima/Audio/Sounds.cs`
- `src/Moongate.Ultima/Multi/Multis.cs`
- `src/Moongate.Ultima/Caching/LruBitmapCache.cs`
- `src/Moongate.Ultima/Caching/LruAnimationCache.cs`
- `src/Moongate.Ultima/Catalog/ItemCatalog.cs`
- `src/Moongate.Ultima/Rendering/BodyRenderer.cs`
- `src/Moongate.Ultima/Rendering/PaperdollComposer.cs`
- `src/Moongate.UO.Data/Version/ClientVersion.cs`

### Tests

- `tests/Moongate.Tests/Ultima/FilesTests.cs`
- `tests/Moongate.Tests/Ultima/TileMatrixTests.cs`
- `tests/Moongate.Tests/Ultima/TileDataTests.cs`
- `tests/Moongate.Tests/Ultima/TexmapsTests.cs`
- `tests/Moongate.Tests/Ultima/BodyRendererTests.cs`
- `tests/Moongate.Tests/Ultima/ClilocTests.cs`
- `tests/Moongate.Tests/Ultima/AsciiFontTests.cs`
- `tests/Moongate.Tests/Ultima/SoundsTests.cs`
- `tests/Moongate.Tests/Ultima/MultisTests.cs`
- `tests/Moongate.Tests/Ultima/ItemCatalogTests.cs`
- `tests/Moongate.Tests/Ultima/PaperdollComposerTests.cs`
- `tests/Moongate.Tests/Ultima/SyntheticAssetFixtureTests.cs`

