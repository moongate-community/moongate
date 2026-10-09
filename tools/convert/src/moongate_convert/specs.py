"""The value types the server's templates write as TOML: a hue or a range of hues, an integer or a range of integers.

Each knows how to read its text as the server does and how to write itself as the server's TOML converters do.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

from .textutil import trim

MAX_HUE = 0xFFFF
_DECIMAL = re.compile(r"[0-9]+", re.ASCII)
_HEX = re.compile(r"[0-9a-fA-F]+", re.ASCII)


@dataclass(frozen=True)
class HueSpec:
    """A hue, or a range of hues one is picked from (``HueSpec`` of the server)."""

    min: int
    max: int
    is_range: bool = False

    @staticmethod
    def from_value(hue: int) -> HueSpec:
        return HueSpec(hue, hue, False)

    @staticmethod
    def from_range(low: int, high: int) -> HueSpec:
        return HueSpec(low, high, True)

    @staticmethod
    def try_parse(text: str | None) -> HueSpec | None:
        """``0x0481``, ``1153``, ``0x0481-0x0489`` or ``hue(1153:1161)``; None for anything else."""
        if text is None or not trim(text):
            return None

        trimmed = trim(text)

        if trimmed[:4].lower() == "hue(" and trimmed.endswith(")"):
            bounds = trimmed[4:-1].split(":")

            if len(bounds) != 2:
                return None
        else:
            bounds = trimmed.split("-")

        if len(bounds) == 1:
            hue = _hue(bounds[0])

            return None if hue is None else HueSpec.from_value(hue)

        if len(bounds) == 2:
            low, high = _hue(bounds[0]), _hue(bounds[1])

            if low is not None and high is not None and low <= high:
                return HueSpec.from_range(low, high)

        return None

    def to_toml(self) -> str:
        """A range is the text ``"0x0481-0x0489"``, a hue its number."""
        if self.is_range:
            return f'"0x{self.min:04X}-0x{self.max:04X}"'

        return str(self.min)


def _hue(text: str) -> int | None:
    trimmed = trim(text)

    if trimmed[:2].lower() == "0x":
        digits = trimmed[2:]
        value = int(digits, 16) if _HEX.fullmatch(digits) else None
    else:
        value = int(trimmed) if _DECIMAL.fullmatch(trimmed) else None

    return value if value is not None and 0 <= value <= MAX_HUE else None


@dataclass(frozen=True)
class RangeValue:
    """An integer, or a range of integers one is picked from (``RangeValueSpec<int>`` of the server)."""

    min: int
    max: int
    is_random: bool = False

    @staticmethod
    def from_value(value: int) -> RangeValue:
        return RangeValue(value, value, False)

    @staticmethod
    def from_range(low: int, high: int) -> RangeValue:
        if low > high:
            raise ValueError(f"{low} is greater than {high}: not a range")

        return RangeValue(low, high, True)

    def to_toml(self) -> str:
        """A range is the text ``"2-5"``, a value its number."""
        if self.is_random:
            return f'"{self.min}-{self.max}"'

        return str(self.min)
