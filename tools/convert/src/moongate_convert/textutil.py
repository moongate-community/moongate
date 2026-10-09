"""Small text helpers shared by the converters: numbers, lines, case, files, loose JSON."""

from __future__ import annotations

import json
import re
from pathlib import Path

_WORD_SPLITTER = re.compile(r"[\s_-]|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")
_SPACE = "[ \\t\\n\\v\\f\\r]*"
_INT = re.compile(rf"{_SPACE}[+-]?[0-9]+{_SPACE}", re.ASCII)
INT32_MIN = -(2**31)
INT32_MAX = 2**31 - 1


# What .NET's ``char.IsWhiteSpace`` (so ``Trim``) takes for a space: Python's ``str.strip`` also takes \x1c to \x1f.
WHITESPACE = " \t\n\v\f\r\x85\xa0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000"


def trim(text: str) -> str:
    """As .NET's ``string.Trim()``."""
    return text.strip(WHITESPACE)


def snake_case(text: str) -> str:
    """``HelloWorld`` becomes ``hello_world``; ``APIResponse`` becomes ``api_response``. As ``StringUtils.ToSnakeCase``."""
    if len(text) < 2:
        return text.lower()

    return "_".join(word.lower() for word in _WORD_SPLITTER.split(text) if word)


def try_int(text: str) -> int | None:
    """Reads an integer as .NET's ``int.TryParse`` does: optional sign and spaces, digits, 32 bits."""
    if not _INT.fullmatch(text):
        return None

    value = int(text.strip(" \t\n\v\f\r"))

    return value if INT32_MIN <= value <= INT32_MAX else None


def is_json_int(value: object) -> bool:
    """Whether a parsed JSON value is a whole number that fits 32 bits (a boolean is not)."""
    return isinstance(value, int) and not isinstance(value, bool) and INT32_MIN <= value <= INT32_MAX


def read_lines(path: Path) -> list[str]:
    """The lines of a text file, split on LF, CRLF and CR only, with no empty line for a final newline."""
    # As File.ReadAllLines: a byte order mark is dropped and bad bytes are replaced, not an error.
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    lines = re.split(r"\r\n|\r|\n", text)

    if lines and lines[-1] == "":
        lines.pop()

    return lines


def write_text(path: Path, text: str) -> None:
    """Writes a file as UTF-8 without a byte order mark and with LF line ends, making its folder."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))


def toml_escaped(text: str) -> str:
    """Escapes a string for a basic TOML string: the backslash and the quote."""
    return text.replace("\\", "\\\\").replace('"', '\\"')


def load_loose_json(path: Path) -> object:
    """Reads a JSON file as ModernUO's reader does: comments and trailing commas are fine."""
    return json.loads(_strip_loose(path.read_text(encoding="utf-8-sig")))


def _strip_loose(text: str) -> str:
    """Removes ``//`` and ``/* */`` comments and the commas before a closing bracket, outside of strings."""
    out: list[str] = []
    i = 0
    n = len(text)

    while i < n:
        c = text[i]

        if c == '"':
            j = _end_of_string(text, i)
            out.append(text[i : j + 1])
            i = j + 1
        elif text.startswith("//", i):
            while i < n and text[i] not in "\r\n":
                i += 1
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            i = n if end < 0 else end + 2
        else:
            out.append(c)
            i += 1

    return _drop_trailing_commas("".join(out))


def _end_of_string(text: str, start: int) -> int:
    """The index of the quote that closes the string opened at ``start`` (the end of the text when it never closes)."""
    j = start + 1

    while j < len(text) and text[j] != '"':
        j += 2 if text[j] == "\\" else 1

    return min(j, len(text) - 1)


def _drop_trailing_commas(text: str) -> str:
    """Drops each comma that only spaces separate from a closing bracket, outside of strings."""
    out: list[str] = []
    i = 0
    n = len(text)

    while i < n:
        c = text[i]

        if c == '"':
            j = _end_of_string(text, i)
            out.append(text[i : j + 1])
            i = j + 1
        elif c == ",":
            k = i + 1

            while k < n and text[k].isspace():
                k += 1

            if k < n and text[k] in "]}":
                i += 1
            else:
                out.append(c)
                i += 1
        else:
            out.append(c)
            i += 1

    return "".join(out)
