"""The TOML text writer: what Tomlyn writes, byte for byte."""

from __future__ import annotations

from moongate_convert import csharp, tomlout


def test_a_multiline_string_keeps_tab_and_every_line_end_raw():
    assert tomlout.multiline("a\tb\r\nc\rd\ne") == '"""a\tb\r\nc\rd\ne"""'


def test_a_multiline_string_escapes_the_other_controls_and_quotes():
    assert tomlout.multiline('x\x01"\\') == '"""x\\u0001\\"\\\\"""'


def test_a_basic_string_escapes_c1_controls_like_tomlyn():
    assert tomlout.basic("a\u0085b") == '"a\\u0085b"'


def test_a_double_literal_that_overflows_is_no_literal():
    root = csharp.parse("class A { double a = 90.0; double b = 1e400; }")
    literals = [csharp.double_value(node) for node in csharp.descendants(root, "real_literal")]

    assert literals == [90.0, None]
