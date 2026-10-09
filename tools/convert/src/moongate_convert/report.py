"""What a conversion leaves out or changes, counted by reason, as the C# ``ConversionReport``."""

from __future__ import annotations

from collections import Counter
from typing import TextIO


class ConversionReport:
    """Counts what a run dropped or changed, by reason; the lines come out sorted by reason."""

    def __init__(self) -> None:
        self._counts: Counter[str] = Counter()

    def count(self, reason: str) -> None:
        self._counts[reason] += 1

    @property
    def lines(self) -> list[tuple[str, int]]:
        return sorted(self._counts.items(), key=lambda pair: pair[0])

    def write(self, output: TextIO) -> None:
        """Prints each reason with how often it happened, as ``  3 x reason``."""
        for reason, count in self.lines:
            output.write(f"  {count} x {reason}\n")
