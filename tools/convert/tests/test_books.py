"""The books converter, with the cases of the C# tests it replaces. The C# packet check of the shipped catalog stays in the C# tests."""

from __future__ import annotations

import json
import os
import tomllib

import pytest

from moongate_convert import books, tomlout


class Workspace:
    """A source and a destination folder, and the converter run between them."""

    def __init__(self, tmp_path, convert):
        self.source = tmp_path / "source"
        self.destination = tmp_path / "destination"
        self.source.mkdir()
        self._convert = convert
        self.last = None

    def write(self, name, text):
        path = self.source / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

        return path

    def run(self):
        self.last = self._convert("modernuo-books", self.source, self.destination)

        return self.last.code

    def path(self, book):
        return self.destination / f"{book}.toml"

    def read(self, book):
        return tomllib.loads(self.path(book).read_text(encoding="utf-8"))

    def files(self):
        return sorted(path.name for path in self.destination.iterdir())


@pytest.fixture
def workspace(tmp_path, convert):
    return Workspace(tmp_path, convert)


def literal(text):
    return json.dumps(text)


def book(name, text):
    return f'class {name} {{ public static readonly BookContent Content = new("Title", "Writer", new BookPageInfo({literal(text)})); }}'


def test_literal_catalog_keeps_title_author_lines_blank_pages_and_distinct_journal_parts(workspace):
    workspace.write(
        "Defined/Journal.cs",
        """
namespace Server.Items;
public class Journal1 : BaseBook
{
    public static readonly BookContent Content = new("Journal", "Writer",
        new BookPageInfo(" first", "", "last"), new BookPageInfo(), new BookPageInfo("end"));
}
public class Journal2 : BaseBook
{
    public static readonly BookContent Content = new BookContent("Journal", "Writer",
        new BookPageInfo("another part"));
}
public class BlankBook : BaseBook { public string Title = "no fixed content"; }
""",
    )

    assert workspace.run() == 0, workspace.last.error

    first = workspace.read("journal1")
    assert (first["title"], first["author"]) == ("Journal", "Writer")
    # An empty line of a page is written as a space: an empty line of the text is a page break. A page with no line is an empty page.
    assert first["content"] == " first\n \nlast\n\n\n\nend"
    assert [len(page) for page in books.paginate(first["content"])] == [3, 0, 1]
    assert workspace.read("journal2")["content"] == "another part"
    assert first["item_template"] == "readable_book"
    assert "item_id" not in first
    assert "translations" not in first
    assert 'content = """' in workspace.path("journal1").read_text(encoding="utf-8")
    assert len(workspace.files()) == 2
    assert "2 books, 4 pages" in workspace.last.output


def test_a_book_takes_the_graphic_of_its_source(workspace):
    workspace.write(
        "Covers.cs",
        """
namespace Server.Items;
public class StatedCover : BaseBook
{
    public static readonly BookContent Content = new("T", "A", new BookPageInfo("x"));
    public StatedCover() : base(0xFF2, false) { }
    public StatedCover(Serial serial) : base(serial) { }
}
public class RandomCover : BaseBook
{
    public static readonly BookContent Content = new("T", "A", new BookPageInfo("x"));
    public RandomCover() : base(Utility.Random(0xFEF, 2), false) { }
}
public class DerivedCover : RedBook
{
    public static readonly BookContent Content = new("T", "A", new BookPageInfo("x"));
    public DerivedCover() : base(false) { }
}
public class UnknownCover : SomethingElse
{
    public static readonly BookContent Content = new("T", "A", new BookPageInfo("x"));
}
""",
    )

    assert workspace.run() == 0, workspace.last.error

    assert workspace.read("stated_cover")["item_id"] == 0x0FF2
    assert workspace.read("random_cover")["item_id"] == 0x0FEF
    assert workspace.read("derived_cover")["item_id"] == 0x0FF1
    assert "item_id" not in workspace.read("unknown_cover")
    assert all(workspace.read(name)["item_template"] == "readable_book" for name in ("stated_cover", "random_cover", "derived_cover", "unknown_cover"))
    # Written as the graphics are read everywhere else: in hexadecimal.
    assert "item_id = 0x0FF2" in workspace.path("stated_cover").read_text(encoding="utf-8")
    assert "item_id" not in workspace.path("unknown_cover").read_text(encoding="utf-8")


def test_a_body_that_opens_with_a_line_end_and_a_kept_translation_are_both_written(workspace):
    workspace.write("Known.cs", book("Known", "\nfirst"))
    assert workspace.run() == 0, workspace.last.error
    with workspace.path("known").open("a", encoding="utf-8") as file:
        file.write('\n[translations.ita]\ntitle = "Titolo"\ncontent = "\\n\\nCorpo"\n')

    assert workspace.run() == 0, workspace.last.error

    known = workspace.read("known")
    assert known["content"] == "\nfirst"
    assert (known["translations"]["ita"]["title"], known["translations"]["ita"]["content"]) == ("Titolo", "\n\nCorpo")


def test_a_body_line_that_looks_like_the_graphic_is_left_alone(workspace):
    workspace.write(
        "Covers.cs",
        """
public class Tricky : BaseBook
{
    public static readonly BookContent Content = new("T", "A", new BookPageInfo("item_id = 5", "end"));
    public Tricky() : base(0xFF2, false) { }
}
""",
    )

    assert workspace.run() == 0, workspace.last.error

    text = workspace.path("tricky").read_text(encoding="utf-8")
    assert workspace.read("tricky")["content"] == "item_id = 5\nend"
    assert 'content = """item_id = 5\n' in text
    assert "item_id = 0x0FF2" in text


def test_a_book_of_more_pages_than_the_client_takes_is_refused(workspace):
    pages = ", ".join(f'new BookPageInfo("p{page}")' for page in range(1, 257))
    workspace.write("Long.cs", f'class Endless\n{{\n    public static readonly BookContent Content = new("T", "A", {pages});\n}}')

    assert workspace.run() == 2
    assert "Endless" in workspace.last.error


def test_string_literals_and_comments_decode_without_treating_literal_dollars_as_variables(workspace):
    workspace.write(
        "Escapes.cs",
        '''
// BookContent Content = new("fake", "fake", new BookPageInfo("fake"));
class UOJournal16b
{
    public static readonly BookContent Content = new(
        "A \\"quoted\\" title", @"A\\B", new BookPageInfo(
            "caf\\u00e8", "price $5; $player_name; ${unknown}",
            "http://example.test/" + "path", """raw \\ text"""));
}
''',
    )

    assert workspace.run() == 0, workspace.last.error

    parsed = workspace.read("uo_journal16b")
    assert parsed["title"] == 'A "quoted" title'
    assert parsed["author"] == "A\\B"
    assert parsed["content"] == "cafè\nprice $$5; $$player_name; $${unknown}\nhttp://example.test/path\nraw \\ text"
    assert parsed["content"].replace("$$", "$") == "cafè\nprice $5; $player_name; ${unknown}\nhttp://example.test/path\nraw \\ text"


@pytest.mark.parametrize(
    "initializer",
    [
        'new(GetTitle(), "Writer", new BookPageInfo("text"))',
        'new("Title", "Writer", new BookPageInfo(GetText()))',
        'new("Title", "Writer", new OtherPage("text"))',
        'new("Title", "Writer", new BookPageInfo($"{GetText()}"))',
        'new("Title", "Writer")',
        'new("Title", "Writer", new BookPageInfo("text")) garbage',
    ],
)
def test_an_unsupported_or_malformed_catalog_is_refused_before_changing_earlier_output(workspace, initializer):
    workspace.write("A.cs", book("Earlier", "kept"))
    assert workspace.run() == 0, workspace.last.error
    previous = workspace.path("earlier").read_bytes()
    workspace.write("A.cs", book("Earlier", "changed"))
    workspace.write("Z.cs", "class Bad { public static readonly BookContent Content = " + initializer + "; }")

    assert workspace.run() == 2
    assert "Z.cs" in workspace.last.error
    assert workspace.path("earlier").read_bytes() == previous
    assert len(workspace.files()) == 1


@pytest.mark.parametrize(("title", "author", "content"), [("", "Writer", "text"), ("Title", "Writer", ""), ("Title", "Writer", "bad\u0000text")])
def test_an_invalid_document_text_is_refused_without_creating_the_destination(workspace, title, author, content):
    workspace.write(
        "Bad.cs",
        f"class Bad {{ public static readonly BookContent Content = new({literal(title)}, {literal(author)}, new BookPageInfo({literal(content)})); }}",
    )

    assert workspace.run() == 2
    assert "Bad.cs" in workspace.last.error
    assert not workspace.destination.exists()


@pytest.mark.parametrize("header", [True, False])
def test_an_oversized_header_or_body_is_refused_without_writing(workspace, header):
    title = "x" * 129 if header else "Title"
    content = "text" if header else "x" * 16385
    workspace.write(
        "Large.cs",
        f'class Large {{ public static readonly BookContent Content = new({literal(title)}, "Writer", new BookPageInfo({literal(content)})); }}',
    )

    assert workspace.run() == 2
    assert "Large.cs" in workspace.last.error
    assert not workspace.destination.exists()


@pytest.mark.parametrize("count", [8193, 9000])
def test_dollar_escaping_over_the_source_limit_is_refused_before_changing_earlier_output(workspace, count):
    workspace.write("A.cs", book("Earlier", "kept"))
    assert workspace.run() == 0
    previous = workspace.path("earlier").read_bytes()
    workspace.write("A.cs", book("Earlier", "changed"))
    workspace.write("Z.cs", book("Dollars", "$" * count))

    assert workspace.run() == 2
    assert workspace.path("earlier").read_bytes() == previous
    assert len(workspace.files()) == 1


def test_dollar_escaping_at_the_source_limit_keeps_the_rendered_text(workspace):
    content = "$" * 8192
    workspace.write("A.cs", book("Dollars", content))

    assert workspace.run() == 0, workspace.last.error

    assert len(workspace.read("dollars")["content"]) == 16384
    assert workspace.read("dollars")["content"].replace("$$", "$") == content


@pytest.mark.parametrize("content", ["\n\nfirst", "a\rb\r\nc", "\nfirst", "face \U0001f600"])
def test_leading_blank_pages_and_line_endings_keep_the_decoded_text(workspace, content):
    workspace.write("A.cs", book("Special", content))

    assert workspace.run() == 0, workspace.last.error

    # The TOML reader of Python reads a CR or CRLF as a line feed; the file keeps them, as Tomlyn writes them.
    assert workspace.read("special")["content"] == content.replace("\r\n", "\n").replace("\r", "\n")
    assert "\r" not in content or content.encode() in workspace.path("special").read_bytes()


@pytest.mark.parametrize(("content", "title", "author"), [("\\uD800", "Title", "Writer"), ("text", "\\uD800", "Writer"), ("text", "Title", "\\uDC00")])
def test_an_invalid_unicode_is_refused_before_changing_earlier_output(workspace, content, title, author):
    workspace.write("A.cs", book("Earlier", "kept"))
    assert workspace.run() == 0
    previous = workspace.path("earlier").read_bytes()
    workspace.write("A.cs", book("Earlier", "changed"))
    workspace.write("Z.cs", f'class Bad {{ public static readonly BookContent Content = new("{title}", "{author}", new BookPageInfo("{content}")); }}')

    assert workspace.run() == 2
    assert workspace.path("earlier").read_bytes() == previous
    assert len(workspace.files()) == 1


def test_a_surrogate_pair_escape_is_one_character(workspace):
    workspace.write("A.cs", book("Pair", "\\ud83d\\ude00").replace('"\\\\ud83d\\\\ude00"', '"\\ud83d\\ude00"'))

    assert workspace.run() == 0, workspace.last.error
    assert workspace.read("pair")["content"] == "\U0001f600"


def test_existing_translations_keep_every_field_while_the_english_is_refreshed(workspace):
    workspace.write("A.cs", book("Known", "before"))
    assert workspace.run() == 0
    path = workspace.path("known")
    text = path.read_text(encoding="utf-8")
    text += "[translations]\n[translations.fre]\nauthor = " + tomlout.basic("Autrice") + "\n"
    text += "[translations.ita]\ntitle = " + tomlout.basic("Titolo") + "\ncontent = " + tomlout.basic("\n\nCorpo\r\nletterale $$5") + "\n"
    path.write_text(text, encoding="utf-8", newline="")
    workspace.write("A.cs", book("Known", "after"))

    assert workspace.run() == 0, workspace.last.error

    known = workspace.read("known")
    assert known["content"] == "after"
    assert sorted(known["translations"]) == ["fre", "ita"]
    assert known["translations"]["ita"] == {"title": "Titolo", "content": "\n\nCorpo\r\nletterale $$5"}
    assert known["translations"]["fre"] == {"author": "Autrice"}
    before = path.read_bytes()
    assert workspace.run() == 0
    assert path.read_bytes() == before


@pytest.mark.parametrize("language", ["eng", "ita", "fre", "ger", "spa", "por", "pol", "cze"])
def test_a_supported_translation_keeps_literal_dollars_and_uses_the_new_english_fallback(workspace, language):
    workspace.write("A.cs", book("Known", "before"))
    assert workspace.run() == 0
    with workspace.path("known").open("a", encoding="utf-8") as file:
        file.write(f'\n[translations.{language}]\ntitle = "Price $$5"\n')
    workspace.write("A.cs", book("Known", "after"))

    assert workspace.run() == 0, workspace.last.error

    assert workspace.read("known")["translations"][language]["title"] == "Price $$5"
    assert workspace.read("known")["content"] == "after"


@pytest.mark.parametrize(
    ("language", "fields"),
    [
        ("rus", 'content = "text"'),
        ("ita", 'content = ""'),
        ("ita", 'title = ""'),
        ("ita", 'content = "$unknown"'),
        ("ita", 'content = "$player_name"'),
        ("ita", 'content = "bad\\u0000text"'),
        ("ita", 'content = "bad\\uD800text"'),
        ("ita", "content = 123"),
        ("ita", 'content = "unterminated'),
    ],
)
def test_an_invalid_existing_translation_is_refused_before_changing_earlier_books(workspace, language, fields):
    workspace.write("A.cs", book("Earlier", "kept"))
    workspace.write("Z.cs", book("Known", "before"))
    assert workspace.run() == 0
    earlier = workspace.path("earlier")
    previous = earlier.read_bytes()
    path = workspace.path("known")
    with path.open("a", encoding="utf-8") as file:
        file.write(f"\n[translations.{language}]\n{fields}\n")
    invalid = path.read_bytes()
    workspace.write("A.cs", book("Earlier", "changed"))

    assert workspace.run() == 2
    assert earlier.read_bytes() == previous
    assert path.read_bytes() == invalid
    assert "known" in workspace.last.error


@pytest.mark.parametrize("header", [True, False])
def test_an_existing_translation_over_the_text_limits_is_refused_before_writing(workspace, header):
    workspace.write("A.cs", book("Known", "before"))
    assert workspace.run() == 0
    text = "x" * 129 if header else "x" * 16385
    with workspace.path("known").open("a", encoding="utf-8") as file:
        file.write(f"\n[translations.ita]\n{'title' if header else 'content'} = {literal(text)}\n")
    previous = workspace.path("known").read_bytes()

    assert workspace.run() == 2
    assert workspace.path("known").read_bytes() == previous


def test_colliding_class_ids_are_both_refused_instead_of_overwriting(workspace):
    workspace.write("A.cs", book("MyBook", "first"))
    workspace.write("B.cs", book("My_Book", "second"))

    assert workspace.run() == 2
    assert "duplicate" in workspace.last.error
    assert not workspace.destination.exists()


def test_a_repeated_conversion_is_deterministic_and_keeps_unrelated_files(workspace):
    workspace.write("A.cs", book("Known", "first"))
    assert workspace.run() == 0, workspace.last.error
    path = workspace.path("known")
    before = path.read_bytes()
    unrelated = workspace.path("welcome_letter")
    unrelated.write_text("custom letter", encoding="utf-8")

    assert workspace.run() == 0, workspace.last.error
    assert path.read_bytes() == before
    assert unrelated.read_text(encoding="utf-8") == "custom letter"
    workspace.write("A.cs", book("Known", "second"))
    assert workspace.run() == 0, workspace.last.error
    assert workspace.read("known")["content"] == "second"
    assert unrelated.read_text(encoding="utf-8") == "custom letter"


def test_an_output_name_taken_by_a_folder_is_refused_before_changing_other_books(workspace):
    workspace.write("A.cs", book("First", "kept"))
    assert workspace.run() == 0, workspace.last.error
    workspace.write("A.cs", book("First", "changed"))
    workspace.write("Z.cs", book("Last", "new"))
    workspace.path("last").mkdir()

    assert workspace.run() == 2
    assert workspace.read("first")["content"] == "kept"
    assert "last.toml" in workspace.last.error


def test_an_output_file_symlink_is_not_followed(workspace):
    workspace.write("A.cs", book("Known", "new"))
    workspace.destination.mkdir()
    target = workspace.write("outside.txt", "preserved")
    os.symlink(target, workspace.path("known"))

    assert workspace.run() == 2
    assert target.read_text(encoding="utf-8") == "preserved"


def test_an_empty_or_missing_source_is_a_usage_error_without_a_destination(workspace):
    assert workspace.run() == 2
    assert "no static books" in workspace.last.error
    workspace.source.rmdir()
    assert workspace.run() == 2
    assert "does not exist" in workspace.last.error
    assert not workspace.destination.exists()


@pytest.mark.parametrize(
    ("name", "expected"),
    [("Journal1", "journal1"), ("UOJournal16b", "uo_journal16b"), ("My_Book", "my_book"), ("MyBook", "my_book"), ("BirdsOfBritannia", "birds_of_britannia")],
)
def test_the_id_of_a_class_is_its_snake_case_name(name, expected):
    assert books.snake_lower(name) == expected
