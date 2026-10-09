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


class UoxWorkspace:
    """The folders of a ``uox`` run in a temporary folder: the source ``.dfn`` files, the destinations and UOX3's ``js`` folder."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.source = root / "source"
        self.destination = root / "destination"
        self.loot_destination = root / "loot-destination"
        self.mobile_source = root / "dfndata"
        self.mobile_destination = root / "mobile-destination"
        self.names_destination = root / "names" / "names.toml"
        self.starting_items_destination = root / "starting" / "starting_items.toml"
        self.scripts_source = root / "js"
        self.npc_lists_destination = root / "npc_lists"
        self.spawns_destination = root / "spawns"
        self.source.mkdir(parents=True)
        self.output = io.StringIO()
        self.error = io.StringIO()

    @property
    def combined(self) -> str:
        return self.output.getvalue() + self.error.getvalue()

    def write_source(self, relative: str, content: str) -> Path:
        return self._write(self.source / relative, content)

    def write_scripts(self, relative: str, content: str) -> Path:
        return self._write(self.scripts_source / relative, content)

    def run(self, loot: bool = True, scripts: bool = False) -> int:
        """Runs the ``uox`` command on the source folder, with the loot destination and the scripts source when asked."""
        from moongate_convert import uox

        return uox.run(
            self.source,
            self.destination,
            self.loot_destination if loot else None,
            self.output,
            self.error,
            scripts_source=self.scripts_source if scripts else None,
        )

    def items(self, file: str = "items.toml") -> dict[str, dict]:
        """The item templates of a destination file by id."""
        return {item["id"]: item for item in read_toml(self.destination / file)["item"]}

    def loot(self, name: str) -> dict:
        """The one loot table of a loot destination file."""
        (table,) = read_toml(self.loot_destination / f"{name}.toml")["loot"]

        return table

    @staticmethod
    def _write(path: Path, content: str) -> Path:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

        return path


@pytest.fixture
def uox_workspace(tmp_path: Path) -> UoxWorkspace:
    return UoxWorkspace(tmp_path / "uox")
