"""Reads ModernUO's C# as syntax with tree-sitter, as the C# converters read it with Roslyn: nothing is compiled or run.

The helpers answer the questions the converters ask of Roslyn's nodes: the name of a class, the arguments of a call, the
value of a literal. A literal has the value the C# compiler gives it, so an escape is decoded and a number is an ``int``
only when the compiler would type it so.
"""

from __future__ import annotations

import math
import re
from collections.abc import Iterator
from pathlib import Path

import tree_sitter_c_sharp
from tree_sitter import Language, Node, Parser

_PARSER = Parser(Language(tree_sitter_c_sharp.language()))
_ESCAPES = {"'": "'", '"': '"', "\\": "\\", "0": "\0", "a": "\a", "b": "\b", "f": "\f", "n": "\n", "r": "\r", "t": "\t", "v": "\v"}
_ESCAPE = re.compile(r"\\(?:u([0-9A-Fa-f]{4})|U([0-9A-Fa-f]{8})|x([0-9A-Fa-f]{1,4})|(.))", re.DOTALL)
_PAIR = re.compile("[\ud800-\udbff][\udc00-\udfff]")
_SUFFIX = re.compile(r"(?i)(ul|lu|u|l)$")


class SourceError(Exception):
    """The C# cannot be read: a syntax error, or something that is no literal. The text is the message of the converter."""


def read_source(path: Path) -> str:
    """The text of a C# file, as ``File.ReadAllText`` with a strict UTF-8: a byte order mark is dropped, a bad byte is an error."""
    return path.read_bytes().decode("utf-8-sig")


def parse(text: str) -> Node:
    """The syntax tree of a C# source; the caller decides whether a syntax error matters (see ``check``)."""
    return _PARSER.parse(text.encode("utf-8")).root_node


def check(root: Node, path: str) -> None:
    """Raises ``SourceError`` when the source has a syntax error, with the place of the first one as Roslyn names it."""
    if not root.has_error:
        return

    node = _first_error(root) or root
    row, column = node.start_point

    raise SourceError(f"{path}({row + 1},{column + 1}): error CS1002: syntax error")


def _first_error(node: Node) -> Node | None:
    if node.type == "ERROR" or node.is_missing:
        return node

    for child in node.children:
        if child.has_error or child.is_missing:
            found = _first_error(child)

            if found is not None:
                return found

    return None


def descendants(node: Node, *types: str) -> Iterator[Node]:
    """Every node under ``node`` of those types, in source order (``DescendantNodes().OfType<>()``)."""
    for child in node.children:
        if child.type in types:
            yield child

        yield from descendants(child, *types)


def ancestors(node: Node) -> Iterator[Node]:
    """The parents of a node, the nearest first."""
    parent = node.parent

    while parent is not None:
        yield parent

        parent = parent.parent


def members(owner: Node, *types: str) -> list[Node]:
    """The direct members of a class of those types (``Members.OfType<>()``)."""
    body = next((child for child in owner.children if child.type == "declaration_list"), None)

    return [child for child in body.children if child.type in types] if body is not None else []


def text(node: Node) -> str:
    """The source text of a node."""
    return node.text.decode("utf-8") if node.text is not None else ""


def name_of(owner: Node) -> str:
    """The name of a class or method declaration; the identifier right before its parameters or base list."""
    field = owner.child_by_field_name("name")

    if field is not None:
        return text(field)

    return next((text(child) for child in owner.children if child.type == "identifier"), "")


def modifiers(node: Node) -> set[str]:
    """The modifier keywords of a declaration: ``static``, ``readonly``..."""
    return {text(child) for child in node.children if child.type == "modifier"}


def named_children(node: Node) -> list[Node]:
    """The children that are not punctuation or comments."""
    return [child for child in node.children if child.is_named and child.type != "comment"]


def creation_type(node: Node) -> Node | None:
    """The type of an ``object_creation_expression``."""
    return node.child_by_field_name("type")


def argument_list(node: Node) -> Node | None:
    """The ``argument_list`` of a call, a creation or a constructor initializer; None when it has none."""
    return next((child for child in node.children if child.type == "argument_list"), None)


def arguments(node: Node) -> list[Node]:
    """The ``argument`` nodes of a call or a creation (empty when it has no list)."""
    arguments_ = argument_list(node)

    return [child for child in arguments_.children if child.type == "argument"] if arguments_ is not None else []


def is_plain_argument(argument: Node) -> bool:
    """Whether an argument has no name and no ``ref``, ``out`` or ``in``: it is just an expression."""
    kids = argument.children

    if len(kids) >= 2 and kids[0].type == "identifier" and kids[1].type == ":":
        return False

    return not any(child.type in ("ref", "out", "in") for child in kids)


def expression(argument: Node) -> Node:
    """The expression of an argument (the last named child)."""
    return named_children(argument)[-1]


def strip_parentheses(node: Node) -> Node:
    """The expression inside any parentheses."""
    while node.type == "parenthesized_expression":
        node = named_children(node)[0]

    return node


def type_matches(node: Node | None, name: str) -> bool:
    """Whether a type is ``name`` or ``Namespace.name`` or ``alias::name`` (``IsType``)."""
    if node is None:
        return False

    if node.type == "identifier":
        return text(node) == name

    if node.type == "qualified_name":
        return text(named_children(node)[-1]) == name

    if node.type == "alias_qualified_name":
        return text(named_children(node)[-1]) == name

    return False


def last_name(node: Node) -> str | None:
    """The name a member access ends with (``SkillName.Magery`` is ``Magery``); None for anything else."""
    if node.type == "member_access_expression":
        return text(named_children(node)[-1])

    return None


# --- literals ---


def string_value(node: Node) -> str | None:
    """The value of a string literal, regular, verbatim or raw; None when the node is no string literal."""
    if node.type == "string_literal":
        return _fuse(_ESCAPE.sub(_unescape, _inner(text(node), '"', '"', 1)))

    if node.type == "verbatim_string_literal":
        return _fuse(text(node)[2:-1].replace('""', '"'))

    if node.type == "raw_string_literal":
        return _raw(text(node))

    return None


def _inner(source: str, start: str, end: str, size: int) -> str:
    return source[size:-size] if source.startswith(start) and source.endswith(end) else source


def _unescape(match: re.Match[str]) -> str:
    unit, wide, hexa, simple = match.groups()

    if unit is not None:
        return chr(int(unit, 16))

    if wide is not None:
        value = int(wide, 16)

        return chr(value) if value <= 0x10FFFF else "�"

    if hexa is not None:
        return chr(int(hexa, 16))

    return _ESCAPES.get(simple, simple)


def _fuse(value: str) -> str:
    """Joins the surrogate pairs of ``\\uD83D\\uDE00`` into the character they make; a lone surrogate stays, as in .NET."""
    return _PAIR.sub(lambda pair: pair.group().encode("utf-16", "surrogatepass").decode("utf-16"), value)


def _raw(source: str) -> str:
    quotes = len(source) - len(source.lstrip('"'))
    body = source[quotes : len(source) - quotes]

    if "\n" not in body and "\r" not in body:
        return body

    lines = body.replace("\r\n", "\n").split("\n")
    # The closing delimiter's own line gives the indentation every line loses.
    indent = re.match(r"[ \t]*", lines[-1]).group()

    return "\n".join(line[len(indent) :] if line.startswith(indent) else line.lstrip(" \t") for line in lines[1:-1])


def literal_text(node: Node) -> str:
    """The value of a string made of literals, parentheses and ``+``; raises ``SourceError`` for anything that runs."""
    node = strip_parentheses(node)
    value = string_value(node)

    if value is not None:
        return value

    if node.type == "binary_expression":
        parts = named_children(node)
        operator = next((child for child in node.children if not child.is_named), None)

        if len(parts) == 2 and operator is not None and text(operator) == "+":
            return literal_text(parts[0]) + literal_text(parts[1])

    raise SourceError("Only literal strings are supported; runtime expressions are not executed.")


def int_value(node: Node) -> int | None:
    """The value of an integer literal the compiler types as ``int``; None for anything else (a suffix, a wider number, a name)."""
    if node.type != "integer_literal":
        return None

    source = text(node).replace("_", "")

    if _SUFFIX.search(source):
        return None

    value = int(source[2:], 16) if source[:2].lower() == "0x" else int(source[2:], 2) if source[:2].lower() == "0b" else int(source)

    return value if value <= 0x7FFFFFFF else None


def double_value(node: Node) -> float | None:
    """The value of a real literal typed ``double`` (``90.0``); None for an integer, a float, a decimal or anything else."""
    if node.type != "real_literal":
        return None

    source = text(node).replace("_", "")

    if source[-1] in "fFmM":
        return None

    value = float(source.rstrip("dD"))

    # Roslyn refuses a literal that overflows a double (1e400); tree-sitter reads it.
    return value if math.isfinite(value) else None


def utf16_length(value: str) -> int:
    """The ``Length`` of a .NET string: UTF-16 code units, so a character beyond the basic plane counts twice."""
    return len(value) + sum(1 for character in value if ord(character) > 0xFFFF)
