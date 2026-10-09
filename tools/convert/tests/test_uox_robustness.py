"""What the ``uox`` command does with a destination or a source that is not what it expects: a message and exit 2, or the C# result."""

from __future__ import annotations

import os

import pytest

ITEM = "[base_torch]\n{\nid=0x0f6b\n}\n"
root = pytest.mark.skipif(os.geteuid() == 0, reason="root reads every folder")


def test_a_foreign_toml_in_the_destination_without_an_id_does_not_stop_the_read_back(uox_workspace):
    uox_workspace.write_source("a.dfn", ITEM)
    (uox_workspace.destination / "old").mkdir(parents=True)
    (uox_workspace.destination / "old" / "old.toml").write_text('[[item]]\nname = "x"\n', encoding="utf-8")

    assert uox_workspace.run() == 0, uox_workspace.error.getvalue()


@pytest.mark.parametrize("text", ["item = 5\n", "[item]\nid = \"x\"\n", "item = [1, 2]\n"])
def test_a_foreign_toml_of_another_shape_in_the_destination_is_not_an_item(uox_workspace, text):
    uox_workspace.write_source("a.dfn", ITEM)
    uox_workspace.destination.mkdir(parents=True)
    (uox_workspace.destination / "old.toml").write_text(text, encoding="utf-8")

    assert uox_workspace.run() == 0, uox_workspace.error.getvalue()


def test_a_directory_link_in_the_source_is_followed(uox_workspace, tmp_path):
    uox_workspace.write_source("a.dfn", ITEM)
    outside = tmp_path / "outside"
    outside.mkdir()
    (outside / "b.dfn").write_text("[base_lamp]\n{\nid=0x0a15\n}\n", encoding="utf-8")
    (uox_workspace.source / "link").symlink_to(outside, target_is_directory=True)

    assert uox_workspace.run() == 0
    assert (uox_workspace.destination / "link" / "b.toml").is_file()


@root
def test_an_unreadable_folder_in_the_source_is_an_error_not_a_gap(uox_workspace):
    uox_workspace.write_source("a.dfn", ITEM)
    locked = uox_workspace.source / "locked"
    locked.mkdir()
    locked.chmod(0)

    try:
        assert uox_workspace.run() == 2
        assert uox_workspace.error.getvalue().strip()
        assert not uox_workspace.destination.exists()
    finally:
        locked.chmod(0o700)


@root
def test_an_unreadable_scripts_file_is_an_error(uox_workspace):
    uox_workspace.write_source("a.dfn", ITEM)
    scripts = uox_workspace.write_scripts("jse_objectassociations.scp", "")
    scripts.chmod(0)

    try:
        assert uox_workspace.run(scripts=True) == 2
        assert uox_workspace.error.getvalue().strip()
    finally:
        scripts.chmod(0o600)
