"""The ``.dfn`` reader and the number reader of UOX3, ported from the C# ``DfnParserTests``, ``DfnBlockExtensionsTests`` and ``UoxNumberTests``."""

from __future__ import annotations

import pytest

from moongate_convert import dfn
from moongate_convert.dfn import IgnoreCaseDict, IgnoreCaseSet


def block(*lines: str) -> dfn.DfnBlock:
    (parsed,) = dfn.parse(["[x]", "{", *lines, "}"])

    return parsed


def test_parse_keeps_the_comment_of_each_field_and_the_brace_label():
    (parsed,) = dfn.parse(["[orc]", "{ Orc things", "NAME=#//an orc", "TITLE=5052 // the Blacksmith", "STR=96 120", "}"])

    assert parsed.fields["NAME"] == "#"
    assert parsed.comments["NAME"] == "an orc"
    assert parsed.comments["title"] == "the Blacksmith"
    assert "STR" not in parsed.comments
    assert parsed.label == "Orc things"
    assert parsed.entry_comments == ["an orc", "the Blacksmith", None]


def test_parse_a_bare_line_keeps_its_comment():
    (parsed,) = dfn.parse(["[RANDOMNAME 5]", "{", "3009//a daemon", "Imp", "}"])

    assert parsed.entries == ["3009", "Imp"]
    assert parsed.entry_comments == ["a daemon", None]
    assert parsed.label is None


def test_a_comment_glued_onto_a_brace_does_not_hide_the_block():
    (parsed,) = dfn.parse(["[a]", "{//approximately 1%", "id=5", "}"])

    assert parsed.fields["id"] == "5"


def test_lines_outside_a_block_are_ignored_and_a_header_without_a_block_makes_none():
    assert dfn.parse(["id=5", "// comment", "", "[orphan]", "id=6"]) == []


def test_a_tag_with_no_value_is_an_entry_but_not_a_field():
    parsed = block("decay=", "pileable=1")

    assert parsed.entries == ["decay=", "pileable=1"]
    assert "decay" not in parsed.fields
    assert parsed.fields["pileable"] == "1"


def test_fields_are_read_whatever_their_case_and_keep_the_first_spelling():
    parsed = block("NAME=a", "name=b")

    assert list(parsed.fields) == ["NAME"]
    assert parsed.fields["Name"] == "b"


def test_get_targets_splits_a_random_pick_and_is_empty_without_get():
    assert dfn.get_targets(block("GET=graydragon  reddragon")) == ["graydragon", "reddragon"]
    assert dfn.get_targets(block("NAME=an orc")) == []


def test_parent_targets_prefer_getlbr_over_get():
    assert dfn.parent_targets(block("get=base_item", "getlbr=smallbod_oldid")) == ["smallbod_oldid"]
    assert dfn.parent_targets(block("get=base_item")) == ["base_item"]


@pytest.mark.parametrize(
    ("text", "expected"),
    [("12", 12), ("-3", -3), ("0x0F", 15), (" 0x0c4f 0x0c50 ", 0x0C4F), ("0x0x04FC", 0x04FC), ("0x15b6]", 0x15B6), ("0xFFFFFFFF", -1), ("+7", 7)],
)
def test_uox_number_reads_hex_or_decimal_forgiving_uox3_typos(text, expected):
    assert dfn.uox_number(text) == expected


@pytest.mark.parametrize("text", ["", "abc", "0xZZ", "0x", "0x100000000", "2147483648", "1.5", "٣"])
def test_uox_number_not_a_number_is_none(text):
    assert dfn.uox_number(text) is None


def test_ignore_case_set_and_dict():
    names = IgnoreCaseSet(["Foo"])
    names.add("Bar")

    assert "FOO" in names and "bar" in names and "baz" not in names and len(names) == 2

    values: IgnoreCaseDict[int] = IgnoreCaseDict({"A": 1})
    values["a"] = 2

    assert dict(values.items()) == {"A": 2}
    assert values.get("a") == 2 and "A" in values and values.copy() is not values
