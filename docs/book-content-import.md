# Import book texts

`mgctl convert modernuo-books` reads the static book texts shipped in ModernUO and writes
Moongate [readable document templates](data-files/books.md). It parses C# syntax without
compiling or running emulator code. These documents are [books](data-files/books.md#books-and-parchments):
they open the client's own book, read only, each with the cover of its source.

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

| Source under `Projects/UOContent` | Books | Source pages |
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
Leading spaces, spelling and annotations stay as written. Lines are joined with a newline
and source pages with two newlines; an empty line inside a ModernUO page is written as a line
of one space. This preserves the source page breaks in the stored text, including empty
source pages, but displayed pagination follows Moongate's native book limits: 79 UTF-16 code units
per line and 8 lines per page. Longer lines wrap and longer source pages continue onto another
displayed page. Trailing newlines are trimmed when opening a book, so final empty source pages
are not displayed. For example, `children_tales_vol2` has 10 source pages and displays 9;
the shipped English catalog displays 737 pages in total.

The UTF-8 TOML body is multiline when that representation preserves the text exactly;
leading newlines and CR/CRLF sequences use escaped basic strings when needed. Invalid
Unicode is rejected, and serialized fields are checked by deserializing them before writing.
The importer validates the escaped source limit and doubles literal `$` characters so the
[template formatter](data-files/books.md#variables) displays them literally. Imported books
require no variable values or attachments and use `item_template = "readable_book"`.

The cover is the graphic the class gives its base constructor, written as `item_id`: a literal
(`base(0xFF2, false)`), the first of a random pair (`Utility.Random(0xFEF, 2)` gives `0x0FEF`), or
that of the kind of book the class derives from (`RedBook`, `BlueBook`, `BrownBook`, `TanBook`). A
class that tells none gets no `item_id` and shows the red cover of `readable_book`. The shipped
catalog has 28 brown, 31 red and 3 blue books. A text that needs more than 255 pages of 8 lines,
in English or in a kept translation, rejects the conversion.

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

The shipped translations follow the English source page breaks and use the width of the
book's longest English line as a wrapping target. An unbreakable word can exceed that target.
They retain the four-space indent where the English page opens a paragraph. A translated
source page may need more lines and displayed pages than its English counterpart; the same
native book pagination limits apply when it is opened.

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
