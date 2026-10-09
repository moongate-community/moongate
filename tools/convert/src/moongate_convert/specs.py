"""The value types the server's templates write as TOML: a hue or a range of hues, an integer or a range of integers.

Each knows how to read its text as the server does and how to write itself as the server's TOML converters do.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

from . import tomlout
from .textutil import WHITESPACE, trim

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


_INT32_MIN = -(2**31)
_INT32_MAX = 2**31 - 1
_PRECEDENCE = {"(": 1, "+": 2, "-": 2, "*": 3, "/": 3, "k": 4, "d": 5}
_NUMBER = re.compile(r"[+-]?[0-9]+", re.ASCII)


@dataclass(frozen=True)
class DiceSpec:
    """A number rolled with dice notation (``1d25+95``), or a constant (``DiceSpec`` of the server).

    A uniform range from a to b is one die, ``1d(b-a+1)+(a-1)``: 96 to 120 is ``1d25+95``. A constant is written as its number, an expression
    as the text it was parsed from.
    """

    min: int
    max: int
    text: str | None = None

    @property
    def is_constant(self) -> bool:
        return self.text is None

    @staticmethod
    def from_value(value: int) -> DiceSpec:
        return DiceSpec(value, value, None)

    @staticmethod
    def try_parse(text: str | None) -> DiceSpec | None:
        """An integer, negative allowed, or a dice expression the server's parser accepts; None for anything else."""
        if text is None or not trim(text):
            return None

        trimmed = trim(text)

        if _NUMBER.fullmatch(trimmed):
            value = int(trimmed)

            return DiceSpec.from_value(value) if _INT32_MIN <= value <= _INT32_MAX else None

        try:
            low, high = _dice_bounds(trimmed)
        except Exception:  # noqa: BLE001 - the server's parser fails the same way on any malformed expression
            return None

        return DiceSpec(low, high, trimmed)

    def to_toml(self) -> str:
        """A constant is its number, an expression its text."""
        return str(self.min) if self.is_constant else tomlout.basic(self.text or "")


def _checked(value: int) -> int:
    if not _INT32_MIN <= value <= _INT32_MAX:
        raise OverflowError(value)

    return value


def _postfix(infix: str) -> list[str]:
    """The tokens of an expression in postfix order, as the server's ``Parser.ToPostfix``."""
    output: list[str] = []
    operators: list[str] = []
    index = 0
    last_was_operator = True

    while index < len(infix):
        character = infix[index]

        if character in WHITESPACE:
            index += 1

            continue

        if character.isdigit() or (last_was_operator and character == "-"):
            last_was_operator = False
            number = character
            index += 1

            while index < len(infix) and infix[index].isdigit():
                number += infix[index]
                index += 1

            output.append(number)

            continue

        last_was_operator = True

        if character == "(":
            operators.append(character)
        elif character == ")":
            if "(" not in operators:
                raise ValueError("unbalanced")

            operator = operators.pop()

            while operator != "(":
                output.append(operator)
                operator = operators.pop()

            last_was_operator = False
        elif character in _PRECEDENCE:
            while operators and _PRECEDENCE[operators[-1]] >= _PRECEDENCE[character]:
                output.append(operators.pop())

            operators.append(character)
        else:
            raise ValueError("unknown operator")

        index += 1

    while operators:
        operator = operators.pop()

        if operator == "(":
            raise ValueError("unbalanced")

        output.append(operator)

    return output


def _dice_bounds(expression: str) -> tuple[int, int]:
    """The smallest and the largest roll of an expression; raises when the server's parser would."""
    stack: list[tuple] = []

    for token in _postfix(expression):
        if token[0].isdigit() or len(token) > 1:
            if not _NUMBER.fullmatch(token):
                raise ValueError("number")

            stack.append(("constant", _checked(int(token))))

            continue

        second, first = stack.pop(), stack.pop()

        if token == "k":
            if first[0] != "dice":
                raise ValueError("keep needs dice")

            stack.append(("keep", second, first))
        else:
            kind = {"d": "dice", "*": "multiply", "/": "divide", "+": "add", "-": "subtract"}[token]
            stack.append((kind, first, second))

    if len(stack) != 1:
        raise ValueError("syntax")

    return _bounds(stack[0])


def _bounds(term: tuple) -> tuple[int, int]:
    kind = term[0]

    if kind == "constant":
        return term[1], term[1]

    if kind == "keep":
        keep_low, keep_high = _bounds(term[1])
        dice = term[2]
        count_low, _ = _bounds(dice[1])
        _, sides_high = _bounds(dice[2])
        _bounds(dice)

        if keep_low < 0 or keep_high > count_low:
            raise ValueError("choose")

        return keep_low, _checked(keep_high * sides_high)

    low1, high1 = _bounds(term[1])
    low2, high2 = _bounds(term[2])

    if kind == "dice":
        if low1 < 0 or low2 <= 0:
            raise ValueError("dice")

        return low1, _checked(high1 * high2)

    if kind == "add":
        return _checked(low1 + low2), _checked(high1 + high2)

    if kind == "subtract":
        return _checked(low1 - high2), _checked(high1 - low2)

    if kind == "multiply":
        products = [_checked(low1 * low2), _checked(low1 * high2), _checked(high1 * low2), _checked(high1 * high2)]

        return min(products), max(products)

    if low2 <= 0 <= high2:
        raise ZeroDivisionError

    quotients = [round(a / b) for a in (low1, high1) for b in (low2, high2)]

    return min(quotients), max(quotients)
