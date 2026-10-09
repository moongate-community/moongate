"""Writes TOML text the way the server's own serializer (Tomlyn) writes it, so the converters' files match the C# ones byte for byte.

Keys are written in the order they are given. A table is written by its header and its keys with no blank line, and each
element of an array of tables after the first is preceded by a blank line. A string is a basic string: the quote and the backslash are
escaped, and so are the control characters; everything else, accents and emoji included, is written as it is.
"""

from __future__ import annotations

import re

_SHORT = {"\\": "\\\\", '"': '\\"', "\b": "\\b", "\t": "\\t", "\f": "\\f", "\r": "\\r", "\n": "\\n"}
_CONTROL = re.compile('[\\x00-\\x1f\\x7f"\\\\]')
_MULTILINE_CONTROL = re.compile('[\\x00-\\x09\\x0b\\x0c\\x0e-\\x1f\\x7f"\\\\]')


def basic(text: str) -> str:
    """A TOML basic string, quotes included."""
    return '"' + _CONTROL.sub(_escape, text) + '"'


def multiline(text: str) -> str:
    """A TOML multi-line basic string: the line ends, a carriage return too, stay as they are, so the text stays editable."""
    return '"""' + _MULTILINE_CONTROL.sub(_escape, text) + '"""'


def _escape(match: re.Match[str]) -> str:
    character = match.group()

    return _SHORT.get(character, f"\\u{ord(character):04X}")


def strings(values: list[str]) -> str:
    """A TOML array of basic strings on one line."""
    return "[" + ", ".join(basic(value) for value in values) + "]"
