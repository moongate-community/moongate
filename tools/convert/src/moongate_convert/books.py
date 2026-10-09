"""``modernuo-books``: ModernUO's static ``BookContent`` definitions into one readable document TOML a book.

The C# is read as syntax, nothing is run. What the converter checks of a book is what it can check without the server: the id, the
limits of the texts and the number of pages. That the book also fits the packets of the client is checked by the C# tests of the shipped
catalog (``RepositoryTemplateFilesTests``), which load every file with the server's own renderer; the port does not repeat it.
"""

from __future__ import annotations

import os
import re
import tomllib
import unicodedata
from collections.abc import Iterator
from dataclasses import dataclass, field
from pathlib import Path
from typing import TextIO

from . import csharp, tomlout
from .csharp import SourceError
from .textutil import write_text

HEADER_LIMIT = 128
CONTENT_LIMIT = 16384
UNLIMITED = 2**31 - 1
LINES_PER_PAGE = 8
MAX_PAGES = 255
MAX_LINE_LENGTH = 79
BLANK_LINE = " "
LANGUAGES = ("eng", "ita", "fre", "ger", "spa", "por", "pol", "cze")
_COVERS = {"BrownBook": 0x0FEF, "TanBook": 0x0FF0, "RedBook": 0x0FF1, "BlueBook": 0x0FF2}
_DOCUMENT_TOKEN = re.compile(r"\$\$|\$\{([^}]*)\}|\$([a-z][a-z0-9_]*)")
_GRAPHIC = re.compile(r'^(item_template = "[^"\r\n]*"\n)item_id = (\d+)$', re.MULTILINE)


class _Refused(Exception):
    """A book or a file the converter refuses: the message goes to the error output."""


@dataclass
class Book:
    id: str
    title: str
    author: str
    content: str
    page_count: int
    item_id: int | None = None


@dataclass
class Translation:
    title: str | None = None
    author: str | None = None
    content: str | None = None


@dataclass
class _Document:
    title: str
    author: str
    content: str
    item_id: int | None
    translations: dict[str, Translation] | None = field(default=None)


def run(source: Path, destination: Path, output: TextIO, error: TextIO) -> int:
    if not source.is_dir():
        error.write(f"ModernUO source folder does not exist: {source}\n")

        return 2

    try:
        _reject_link(source)
        _reject_link(destination)

        if destination.is_file():
            raise _Refused(f"Destination is a file: {destination}")

        books: dict[str, Book] = {}

        for path in sorted(_sources(source), key=str):
            for book in read_books(csharp.read_source(path), str(path)):
                if book.id in books:
                    raise _Refused(f"{path}: duplicate book id {book.id}.")

                books[book.id] = book

        if not books:
            raise _Refused(f"{source}: no static books found.")

        # Serialize and validate every target before touching earlier output.
        files: list[tuple[Path, str]] = []

        for book in sorted(books.values(), key=lambda book: book.id):
            path = destination / f"{book.id}.toml"
            _reject_link(path)

            if path.is_dir():
                raise _Refused(f"Output file is a directory: {path}")

            document = _Document(
                _escape_dollars(book.title),
                _escape_dollars(book.author),
                _escape_dollars(book.content),
                book.item_id,
                read_translations(path, book),
            )
            files.append((path, serialize(document, book.id)))

        destination.mkdir(parents=True, exist_ok=True)

        for path, text in files:
            write_text(path, text)

        pages = sum(book.page_count for book in books.values())
        output.write(f"{len(books)} books, {pages} pages converted to {destination}\n")

        return 0
    except (_Refused, SourceError, OSError, UnicodeError, tomllib.TOMLDecodeError) as exception:
        error.write(f"Book conversion failed: {exception}\n")

        return 2


# --- reading the C# ---


def read_books(source: str, path: str) -> list[Book]:
    """The books a C# file defines: each static readonly ``BookContent`` field of a class, read from its literals."""
    root = csharp.parse(source)
    fields = [
        node
        for node in csharp.descendants(root, "field_declaration")
        if csharp.type_matches(_declaration(node).children[0] if _declaration(node) else None, "BookContent")
    ]

    if not fields:
        return []

    csharp.check(root, path)
    books: list[Book] = []

    for node in fields:
        owner = next((parent for parent in csharp.ancestors(node) if parent.type == "class_declaration"), None)
        name = csharp.name_of(owner) if owner is not None else "unknown class"

        try:
            if owner is None or not {"static", "readonly"} <= csharp.modifiers(node):
                raise SourceError("BookContent must be a static readonly class field.")

            for variable in csharp.descendants(_declaration(node), "variable_declarator"):
                books.append(_read_book(variable, owner, name))
        except (SourceError, UnicodeError) as exception:
            raise SourceError(f"{path}: {name}: {exception}") from exception

    return books


def _declaration(field_node):
    return next((child for child in field_node.children if child.type == "variable_declaration"), None)


def _read_book(variable, owner, name: str) -> Book:
    if not any(child.type == "=" for child in variable.children):
        raise SourceError("BookContent has no initializer.")

    value = csharp.named_children(variable)[-1]

    arguments = _arguments(value, "BookContent")

    if len(arguments) < 3:
        raise SourceError("BookContent needs a title, author and pages.")

    title = csharp.literal_text(csharp.expression(arguments[0]))
    author = csharp.literal_text(csharp.expression(arguments[1]))
    # An empty line of the text is a page break, so an empty line inside a page is written as a space: the page stays one page.
    pages = [
        "\n".join(
            BLANK_LINE if not line else line
            for line in (csharp.literal_text(csharp.expression(line)) for line in _arguments(csharp.expression(page), "BookPageInfo"))
        )
        for page in arguments[2:]
    ]
    content = "\n\n".join(pages)

    for part in (title, author, content):
        part.encode("utf-8")

    identifier = snake_lower(name)

    if (
        not _valid_name(identifier)
        or not title.strip()
        or not content.strip()
        or not valid_text(title, HEADER_LIMIT)
        or not valid_text(author, HEADER_LIMIT)
        or not valid_text(content, CONTENT_LIMIT)
        or csharp.utf16_length(content) + content.count("$") > CONTENT_LIMIT
        or paginate(content) is None
    ):
        raise SourceError("Book id or text is invalid or exceeds the document limits.")

    return Book(identifier, title, author, content, len(pages), _graphic(owner))


def _arguments(expression, type_name: str):
    """The arguments of a literal ``new BookContent(...)`` or ``new(...)``; anything else is refused."""
    node = expression
    ok = (
        node.type == "implicit_object_creation_expression"
        or (node.type == "object_creation_expression" and csharp.type_matches(csharp.creation_type(node), type_name))
    ) and not any(child.type == "initializer_expression" for child in node.children)
    arguments = csharp.arguments(node) if ok else None

    if not ok or csharp.argument_list(node) is None or not all(csharp.is_plain_argument(argument) for argument in arguments):
        raise SourceError(f"Expected literal {type_name} constructor arguments.")

    return arguments


def _graphic(owner) -> int | None:
    """The graphic the class gives its base: stated in its constructor, the first of a random pair there, or that of the kind of book it derives from."""
    constructors = [
        constructor
        for constructor in csharp.members(owner, "constructor_declaration")
        if any(child.type == "constructor_initializer" and any(part.type == "base" for part in child.children) for child in constructor.children)
    ]
    constructors.sort(key=_parameter_count)

    for constructor in constructors:
        initializer = next(child for child in constructor.children if child.type == "constructor_initializer")
        arguments = csharp.arguments(initializer)

        if not arguments:
            continue

        first = csharp.expression(arguments[0])

        if first.type == "invocation_expression" and csharp.last_name(first.children[0]) == "Random":
            inner = csharp.arguments(first)
            first = csharp.expression(inner[0]) if inner else first

        value = csharp.int_value(first)

        if value is not None and 1 <= value <= 0xFFFF:
            return value

    base_list = next((child for child in owner.children if child.type == "base_list"), None)

    if base_list is not None:
        for child in base_list.children:
            if child.type == "identifier" and csharp.text(child) in _COVERS:
                return _COVERS[csharp.text(child)]

    return None


def _parameter_count(constructor) -> int:
    parameters = next((child for child in constructor.children if child.type == "parameter_list"), None)

    return sum(1 for child in parameters.children if child.type == "parameter") if parameters is not None else 0


def _sources(directory: Path) -> Iterator[Path]:
    for path in sorted(directory.iterdir(), key=str):
        _reject_link(path)

        if path.is_dir():
            yield from _sources(path)
        elif path.suffix.lower() == ".cs":
            yield path


def _reject_link(path: Path) -> None:
    if os.path.islink(path):
        raise _Refused(f"Symbolic links are not supported: {path}")


# --- ids, limits and pages ---


def snake_lower(name: str) -> str:
    """The id of a class as ``JsonNamingPolicy.SnakeCaseLower`` writes it: ``UOJournal16b`` is ``uo_journal16b``, ``My_Book`` is ``my_book``."""
    result: list[str] = []
    state = "start"

    for index, character in enumerate(name):
        category = unicodedata.category(character)

        if category == "Lu":
            if state == "lower" or state == "space":
                result.append("_")
            elif state == "upper" and index + 1 < len(name) and name[index + 1].islower():
                result.append("_")

            result.append(character.lower())
            state = "upper"
        elif category in ("Ll", "Nd"):
            if state == "space":
                result.append("_")

            result.append(character)
            state = "lower"
        elif category == "Zs":
            if state != "start":
                state = "space"
        else:
            result.append(character)
            state = "start"

    return "".join(result)


def _valid_name(name: str) -> bool:
    return bool(name) and "a" <= name[0] <= "z" and all("a" <= c <= "z" or "0" <= c <= "9" or c == "_" for c in name[1:])


def valid_text(value: str, limit: int) -> bool:
    """Within the limit and with no control character but the line ends and the tab (``BookTextValidation.IsValidText``)."""
    return csharp.utf16_length(value) <= limit and not any(
        unicodedata.category(character) == "Cc" and character not in "\n\r\t" for character in value
    )


def paginate(content: str) -> list[list[str]] | None:
    """The pages of a body as the client shows them, or None when there are more than the client takes (``BookPagination``)."""
    result: list[list[str]] = []
    body = content.replace("\r\n", "\n").replace("\r", "\n").rstrip("\n")

    for page in body.split("\n\n"):
        lines = [fitted for line in page.split("\n") for fitted in _fit(line)] if page else []
        start = 0

        while start == 0 or start < len(lines):
            result.append(lines[start : start + LINES_PER_PAGE])
            start += LINES_PER_PAGE

            if len(result) > MAX_PAGES:
                return None

    return result


def _fit(line: str) -> Iterator[str]:
    while csharp.utf16_length(line) > MAX_LINE_LENGTH:
        indent = len(line) - len(line.lstrip(" "))
        space = line.rfind(" ", 0, MAX_LINE_LENGTH + 1)

        if space > indent:
            yield line[:space]
            line = line[space + 1 :]
        else:
            yield line[:MAX_LINE_LENGTH]
            line = line[MAX_LINE_LENGTH:]

    yield line


def _escape_dollars(value: str) -> str:
    return value.replace("$", "$$")


# --- the translations a file already has ---


def read_translations(path: Path, book: Book) -> dict[str, Translation] | None:
    """The translations of an existing file, checked, so a refresh of the English text keeps them; None when there are none."""
    if not path.is_file():
        return None

    try:
        data = tomllib.loads(path.read_bytes().decode("utf-8-sig"))
        section = data.get("translations", {})

        if not isinstance(section, dict):
            raise _Refused("Existing document cannot be deserialized.")

        if not section:
            return None

        translations: dict[str, Translation] = {}

        for language in sorted(section):
            if language not in LANGUAGES:
                raise _Refused(f"Unsupported translation language: {language}.")

            raw = section[language]
            fields = {name: raw.get(name) if isinstance(raw, dict) else None for name in ("title", "author", "content")}

            if not isinstance(raw, dict) or any(value is not None and not isinstance(value, str) for value in fields.values()):
                raise _Refused(f"Invalid translation: {language}.")

            title = fields["title"] if fields["title"] is not None else _escape_dollars(book.title)
            author = fields["author"] if fields["author"] is not None else _escape_dollars(book.author)
            content = fields["content"] if fields["content"] is not None else _escape_dollars(book.content)

            for part in (title, author, content):
                part.encode("utf-8")

            if (
                not valid_text(title, UNLIMITED)
                or not valid_text(author, UNLIMITED)
                or not valid_text(content, CONTENT_LIMIT)
                or any(match.group() != "$$" for text in (title, author, content) for match in _DOCUMENT_TOKEN.finditer(text))
            ):
                raise _Refused(f"Invalid literal translation text: {language}.")

            title, author, content = (_render(text) for text in (title, author, content))

            if (
                not title.strip()
                or not content.strip()
                or not valid_text(title, HEADER_LIMIT)
                or not valid_text(author, HEADER_LIMIT)
                or not valid_text(content, CONTENT_LIMIT)
                or paginate(content) is None
            ):
                raise _Refused(f"Translation exceeds document or packet limits: {language}.")

            translations[language] = Translation(fields["title"], fields["author"], fields["content"])

        return translations
    except (_Refused, UnicodeError, tomllib.TOMLDecodeError) as exception:
        raise _Refused(f"{path}: {exception}") from exception


def _render(text: str) -> str:
    """A document text with no variables: ``$$`` is a dollar."""
    return text.replace("$$", "$")


# --- writing ---


def serialize(document: _Document, book_id: str) -> str:
    """The TOML of a book. Bodies stay editable multi-line texts when that keeps them exactly; else they are plain strings."""
    for body_multiline, translations_multiline in ((True, True), (True, False), (False, False)):
        text = _graphic_in_hex(_write(document, body_multiline, translations_multiline))

        if _matches(text, document):
            return text

    raise _Refused(f"{book_id}: serialized TOML does not preserve the source text.")


def _write(document: _Document, body_multiline: bool, translations_multiline: bool) -> str:
    string = tomlout.multiline if body_multiline else tomlout.basic
    lines = [
        f"title = {tomlout.basic(document.title)}",
        f"author = {tomlout.basic(document.author)}",
        f"content = {string(document.content)}",
        'item_template = "readable_book"',
    ]

    if document.item_id is not None:
        lines.append(f"item_id = {document.item_id}")

    if document.translations is not None:
        lines.append("[translations]")
        text = tomlout.multiline if translations_multiline else tomlout.basic

        for language, translation in document.translations.items():
            lines.append(f"[translations.{language}]")

            if translation.title is not None:
                lines.append(f"title = {tomlout.basic(translation.title)}")

            if translation.author is not None:
                lines.append(f"author = {tomlout.basic(translation.author)}")

            if translation.content is not None:
                lines.append(f"content = {text(translation.content)}")

    return "\n".join(lines) + "\n"


def _graphic_in_hex(text: str) -> str:
    """A graphic reads as the other graphics of the templates do: ``item_id = 0x0FF1``. Only the field right after ``item_template``."""
    return _GRAPHIC.sub(lambda match: f"{match.group(1)}item_id = 0x{int(match.group(2)):04X}", text)


def _lf(value: str | None) -> str | None:
    """A text with every line end as a line feed: the TOML reader of Python normalizes a CRLF and refuses a lone CR, Tomlyn keeps both."""
    return None if value is None else value.replace("\r\n", "\n").replace("\r", "\n")


def _matches(text: str, expected: _Document) -> bool:
    try:
        actual = tomllib.loads(_lf(text) or "")
    except tomllib.TOMLDecodeError:
        return False

    translations = actual.get("translations", {})
    wanted = expected.translations or {}

    return (
        actual.get("title") == expected.title
        and actual.get("author") == expected.author
        and _lf(actual.get("content")) == _lf(expected.content)
        and actual.get("item_template") == "readable_book"
        and actual.get("item_id") == expected.item_id
        and len(translations) == len(wanted)
        and all(
            language in translations
            and translations[language].get("title") == translation.title
            and translations[language].get("author") == translation.author
            and _lf(translations[language].get("content")) == _lf(translation.content)
            for language, translation in wanted.items()
        )
    )
