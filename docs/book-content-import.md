# Import book texts

`mgctl convert modernuo-books` reads the static book texts shipped in ModernUO and writes
Moongate [readable document templates](data-files/books.md). It parses C# syntax without
compiling or running emulator code. These documents use the current parchment reading
interface; native book covers, pages and editing are separate work.

## Convert the catalog

```sh
dotnet run --project src/Moongate.Ctl -c Release -- convert modernuo-books \
  --source <ModernUO>/Projects/UOContent \
  --destination moongate_root/templates/books/modernuo
```

With a released tool, use `mgctl convert modernuo-books` with the same options.
`--source` is a folder, searched recursively for `.cs` files; use the full `Projects/UOContent`
folder to include the Khaldun journals as well as the library. `--destination` is the folder
receiving the generated TOMLs. Source folders/files and output files that are symbolic links
are refused.

The shipped catalog contains **62 books, 738 source pages and 5,635 source lines**, converted
from [ModernUO revision `35e3a31b`](https://github.com/modernuo/ModernUO/tree/35e3a31b4c3af5668f0f0e2d3045b328ffb26b57).

| Source under `Projects/UOContent` | Books | Pages |
| --- | ---: | ---: |
| `Items/Books/Defined/LibraryBooks.cs` | 28 | 486 |
| The other six `Items/Books/Defined/*.cs` files | 6 | 88 |
| `Engines/Khaldun/Books/GrimmochJournal.cs` | 9 | 46 |
| `Engines/Khaldun/Books/LysanderNotebook.cs` | 6 | 33 |
| `Engines/Khaldun/Books/TavarasJournal.cs` | 13 | 85 |

The six individual definitions are BlackthornWelcomeBook, DrakovsJournal, FropozJournal,
KaburJournal, NewAquariumBook and TranslatedGargoyleJournal. The source repository's
[license](https://github.com/modernuo/ModernUO/blob/35e3a31b4c3af5668f0f0e2d3045b328ffb26b57/LICENSE)
and existing narrative authorship remain upstream provenance. No source code is copied into
the converter.

## Mapping and reruns

Each static readonly `BookContent` field supplies its title, author and ordered `BookPageInfo`
lines. Literal strings, including C# escaped/verbatim/raw strings and literal concatenation,
are supported. Runtime expressions, missing metadata/pages, invalid text, duplicate IDs and
text that cannot fit the current reading packets reject the conversion with exit code 2.
All parsing, validation and serialization finish before any output file is changed.
A filesystem failure during the writes can leave some files updated; correct the reported
path and rerun.

A class becomes a stable `<snake_case_class>.toml` filename. For example,
`GrammarOfOrcish` becomes `grammar_of_orcish`. Titles are preserved and are never
used for deduplication: the Grimmoch, Lysander and Tavara installments remain distinct.
Empty lines, leading spaces, empty pages, spelling and annotations stay as written.
Lines are joined with a newline and pages with two newlines. This provides readable plain
text; it does not retain a separate native pagination model.

The UTF-8 TOML body is multiline when that representation preserves the text exactly;
leading newlines and CR/CRLF sequences use escaped basic strings when needed. Invalid
Unicode is rejected, and serialized fields are checked by deserializing them before writing.
The importer validates the escaped source limit and doubles literal `$` characters so the
[template formatter](data-files/books.md#variables) displays them literally. Imported books
require no variable values or attachments and use `item_template = "readable_scroll"`.

Each shipped book keeps its English source and includes complete title/body overrides for
Italian (`ita`), French (`fre`), German (`ger`), Spanish (`spa`), Portuguese (`por`),
Polish (`pol`) and Czech (`cze`). Author names remain unchanged. The existing document
service chooses `[localization].language` when creating the item; missing translation
fields fall back to the English source. Changing that setting does not rewrite issued books.

Rerunning replaces matching generated filenames, keeps unrelated files such as the welcome
letter, and does not remove files from an older catalog. Put custom edits in a differently
named template if they must survive reruns.

Existing `translations.<language>` fields in matching generated files are preserved when
reimporting. All eight supported language codes, including optional `eng` overrides, are
accepted; partial overrides use the refreshed English fields as fallback. The importer
validates the existing TOML, Unicode, literal template text, source/rendered bounds and
reading packets before writing any file. Invalid translations stop conversion with exit
code 2 and leave existing output unchanged. Other edited fields are regenerated from
ModernUO; unrelated files remain untouched. Existing translations are retained verbatim
when upstream English changes, so review their meaning after importing a different
upstream revision. No network translation service runs during import.

The shipped translations are laid out as the English source: the same pages, lines wrapped to
the width of the book's longest English line, and the four-space indent where the English page
opens a paragraph. A translated page may run to more lines than its source, since the text is
longer.

The ordinary `mgctl init` workflow copies missing shipped files into existing roots and
preserves files already present. Newly added sources load at the next normal startup.
For an existing root whose catalog predates these translations, copy the desired
`[translations.<language>]` tables from the shipped catalog into its matching files.
Neither `mgctl init` nor a reimport invents missing translations in existing files.
A root that holds the catalog under its first names, `modernuo_<book>.toml`, gets the books again
under the names without the prefix: delete the `modernuo_*.toml` files of `templates/books/modernuo`
by hand, or every book is there twice, and use the new ids in `book.give` and `.book`.
Existing issued documents keep their saved text. No converter connects to the world database
or restarts the server.

To create a readable copy from Lua:

```lua
book.give(player, "grammar_of_orcish")
```

## What the other emulators use

The comparison inspected local emulator checkouts and official distribution repositories.
ModernUO was selected because its entire static catalog is literal and already fits the
Moongate converter workflow. Alternative catalogs overlap but have distinct variants; this
command imports ModernUO alone.

| Emulator | Actual source of the texts | Findings |
| --- | --- | --- |
| ModernUO | Server C# `BookContent` definitions | 62 literal books / 738 pages; complete shipped import |
| ServUO | [Server C# definitions](https://github.com/ServUO/ServUO/blob/658d6b71a3b43aa02839dd893ca4f22c88abbbd3/Scripts/Items/Books/LibraryBooks.cs) | 74 definitions / 856 pages: 72 static after title fallback, two fishing guides have runtime location text. Other localized books use client Cliloc strings |
| UOX3 | [Server DFN catalog](https://github.com/UOX3DevTeam/UOX3/blob/4560ae841bac898817143d7aa95ce59f47ab98e0/data/dfndata/misc/books.dfn) | 65 effective books; CP1252 punctuation, repeated sections with last-definition precedence and page-count anomalies |
| POL | [ModernDistro](https://github.com/polserver/ModernDistro/blob/fbb200c57559e08545e5938b7a72c31f787542a4/pkg/items/sysbook/config/master_library.cfg) / [ClassicDistro](https://github.com/polserver/ClassicDistro/blob/0cb44d16aab859f836652f76a8c802287f308961/pkg/items/sysbook/config/books.cfg) packages, not the core checkout | 46 books in each catalog; `p<page>l<line>` keys, blank-line padding and different variants |
| Sphere | [Scripts-X `sp_tm_book.scp`](https://github.com/Sphereserver/Scripts-X/blob/27e78bc896da239d3738fe02a6d6bf8e9045c16d/templates_special/sp_tm_book.scp), not Source-X itself | 45 books / 536 defined page sections; malformed header, missing/out-of-range pages and metadata discrepancies |

Player-written world-save books, blank books, skill-teaching scripts and Cliloc-only documents
are outside this static lore import.
