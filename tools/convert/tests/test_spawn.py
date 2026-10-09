"""Direct tests of the shared spawn helpers."""

from __future__ import annotations

import pytest

from moongate_convert.spawn import minutes


@pytest.mark.parametrize(
    ("value", "expected"),
    [("00:05:00", 5), ("0:30", 30), ("1.02:00:00", 1560), ("2", 2880), ("00:00:10", 1), ("bad", 1), ("00:99:00", 1), (None, 1), (5, 1)],
)
def test_minutes_reads_a_time_span_and_floors_at_a_minute(value, expected):
    assert minutes({"d": value}, "d") == expected
