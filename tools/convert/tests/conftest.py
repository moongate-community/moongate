from __future__ import annotations

import io
import tomllib
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

import pytest

from moongate_convert.cli import main


@dataclass
class Result:
    code: int
    output: str
    error: str


@pytest.fixture
def convert() -> Callable[..., Result]:
    """Runs ``moongate-convert <command> --source ... --destination ...`` and returns the exit code and what it wrote."""

    def run(command: str, source: Path, destination: Path) -> Result:
        output, error = io.StringIO(), io.StringIO()
        code = main([command, "--source", str(source), "--destination", str(destination)], output, error)

        return Result(code, output.getvalue(), error.getvalue())

    return run


def read_toml(path: Path) -> dict:
    return tomllib.loads(path.read_text(encoding="utf-8"))
