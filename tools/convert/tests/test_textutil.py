"""Direct tests of the text helpers the converters share."""

from __future__ import annotations

import pytest

from moongate_convert.textutil import is_json_int, load_loose_json, read_lines, snake_case, try_int


@pytest.mark.parametrize(
    ("text", "expected"),
    [("42", 42), (" -7 ", -7), ("+3", 3), ("2147483647", 2147483647), ("2147483648", None), ("", None), ("1.5", None), ("٣", None)],
)
def test_try_int_reads_ascii_int32_like_dotnet(text, expected):
    assert try_int(text) == expected


def test_is_json_int_rejects_bool_and_float():
    assert is_json_int(3) and not is_json_int(True) and not is_json_int(3.0)


def test_snake_case_splits_words_and_acronyms():
    assert snake_case("TerMur") == "ter_mur"
    assert snake_case("Old Haven-Cave") == "old_haven_cave"


def test_read_lines_drops_bom_and_splits_every_newline(tmp_path):
    path = tmp_path / "a.txt"
    path.write_bytes(b"\xef\xbb\xbfone\r\ntwo\rthree\nfour")

    assert read_lines(path) == ["one", "two", "three", "four"]


def test_read_lines_replaces_bad_bytes(tmp_path):
    path = tmp_path / "a.txt"
    path.write_bytes(b"ok\xff\n")

    assert read_lines(path)[0].startswith("ok")


def test_loose_json_allows_comments_and_trailing_commas(tmp_path):
    path = tmp_path / "a.json"
    path.write_text('[1, 2, /* x */ 3, // y\n "a//b", ]', encoding="utf-8")

    assert load_loose_json(path) == [1, 2, 3, "a//b"]
