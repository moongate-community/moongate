"""The starting items pass of ``uox``: UOX3's ``newbie/newbie.dfn`` as ``starting_items.toml``, ported from the C# tests."""

from __future__ import annotations

import pytest
from conftest import read_toml
from uox_support import item_index

from moongate_convert import uox, uox_starting_items

ITEMS = """\
[0x0f7a]
{
id=0x0f7a
name=black pearl
}
[black_pearl_alias]
{
get=0x0f7a
}
[0x1f03]
{
id=0x1f03
name=robe
}
[bagofreagents]
{
id=0x0e76
name=bag of reagents
}
[ITEMLIST 6]
{
black_pearl_alias
2|0x1f03
blank
}
"""
EXTRAS = """\
[0x0eed]
{
id=0x0eed
name=gold coin
}
[0x103b]
{
id=0x103b
name=bread loaf
}
[0x1f9e]
{
id=0x1f9e
name=pitcher of water
}
"""


class Run:
    def __init__(self, workspace, newbie: str | None, extras: str | None = None) -> None:
        workspace.write_source("items.dfn", ITEMS)

        if extras is not None:
            workspace.write_source("extras.dfn", extras)

        if newbie is not None:
            path = workspace.mobile_source / "newbie" / "newbie.dfn"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(newbie, encoding="utf-8")

        self.workspace = workspace
        self.destination = workspace.starting_items_destination
        self.code = uox_starting_items.run(
            workspace.mobile_source, self.destination, item_index(workspace.source), workspace.output, workspace.error
        )

    @property
    def sets(self) -> list[dict]:
        return read_toml(self.destination).get("set", [])

    @property
    def text(self) -> str:
        return self.destination.read_text(encoding="utf-8")


def test_best_skill_sections_become_skill_sets_with_pack_and_equip_entries(uox_workspace):
    run = Run(
        uox_workspace,
        "// Alchemy.\n[BESTSKILL 0]\n{\nPACKITEM=0x0f7a,3\n//PACKITEM=0x0f84\nEQUIPITEM=0x1f03,0x4ca\n}\n// Leave Empty\n[BESTSKILL X]\n{\n}\n",
    )

    assert run.code == 0, uox_workspace.combined

    (entry_set,) = run.sets
    assert entry_set["skill"] == "alchemy"
    assert "race" not in entry_set and "gender" not in entry_set
    first, second = entry_set["items"]
    assert (first["items"], first["amount"], first["equip"]) == (["0x0f7a_black_pearl"], 3, False)
    assert (second["items"], second["hue"], second["equip"]) == (["0x1f03_robe"], 0x4CA, True)
    assert "amount" not in second


@pytest.mark.parametrize(
    ("header", "common", "race", "gender"),
    [
        ("DEFAULT ALL", True, None, None),
        ("DEFAULT MALE", False, "human", "male"),
        ("DEFAULT FEMALE", False, "human", "female"),
        ("DEFAULT ELF FEMALE", False, "elf", "female"),
        ("DEFAULT GARG MALE", False, "gargoyle", "male"),
    ],
)
def test_default_sections_become_common_or_race_and_gender_filters(uox_workspace, header, common, race, gender):
    run = Run(uox_workspace, f"[{header}]\n{{\nEQUIPITEM=0x1f03\n}}\n")

    assert run.code == 0, uox_workspace.combined

    (entry_set,) = run.sets
    assert (entry_set["common"], entry_set.get("skill"), entry_set.get("race"), entry_set.get("gender")) == (common, None, race, gender)


def test_list_objects_aliases_and_the_newbie_flag_resolve(uox_workspace):
    run = Run(uox_workspace, "[BESTSKILL 25]\n{\nPACKITEM=listobject6,2\nPACKITEM=bagofreagents,1,0\n}\n")

    assert run.code == 0, uox_workspace.combined

    (entry_set,) = run.sets
    first, second = entry_set["items"]
    assert entry_set["skill"] == "magery"
    # black_pearl_alias has one parent, so it is a template of its own inheriting the pearl.
    assert first["items"] == ["black_pearl_alias", "0x1f03_robe"]
    assert first["amount"] == 2 and "newbie" not in first
    assert second["items"] == ["bagofreagents"] and second["newbie"] is False


@pytest.mark.parametrize(("number", "skill"), [(55, "imbuing"), (56, "mysticism")])
def test_best_skill_numbers_follow_uox3s_own_numbering(uox_workspace, number, skill):
    # UOX3 numbers IMBUING 55 and MYSTICISM 56; SkillType has them the other way round.
    run = Run(uox_workspace, f"[BESTSKILL {number}]\n{{\nPACKITEM=0x0f7a\n}}\n")

    assert run.code == 0, uox_workspace.combined
    assert run.sets[0]["skill"] == skill


def test_a_best_skill_number_that_is_no_skill_is_left_out(uox_workspace):
    run = Run(uox_workspace, "[BESTSKILL 58]\n{\nPACKITEM=0x0f7a\n}\n[BESTSKILL 57]\n{\nPACKITEM=0x0f7a\n}\n")

    assert run.code == 0
    assert [entry_set["skill"] for entry_set in run.sets] == ["throwing"]


def test_an_unresolved_item_is_dropped_and_counted(uox_workspace):
    run = Run(uox_workspace, "[DEFAULT ALL]\n{\nPACKITEM=0x0f7a\nPACKITEM=no_such_item\n}\n")

    assert run.code == 0, uox_workspace.combined

    (entry,) = run.sets[0]["items"]
    assert entry["items"] == ["0x0f7a_black_pearl"]
    assert "1 x unresolved item" in uox_workspace.output.getvalue()


def test_an_unknown_tag_is_counted_and_a_line_with_no_equals_is_ignored(uox_workspace):
    run = Run(uox_workspace, "[DEFAULT ALL]\n{\nPACKITEM=0x0f7a\nGIVEITEM=0x1f03\nstray\n}\n")

    assert run.code == 0
    assert "1 x unknown newbie tag GIVEITEM" in uox_workspace.output.getvalue()
    assert len(run.sets[0]["items"]) == 1


def test_the_common_set_also_gets_moongates_own_gold_bread_and_water_when_the_items_exist(uox_workspace):
    run = Run(uox_workspace, "[BESTSKILL 0]\n{\nPACKITEM=0x0f7a\n}\n[DEFAULT ALL]\n{\nPACKITEM=0x0f7a\n}\n", EXTRAS)

    assert run.code == 0, uox_workspace.combined

    # UOX3 keeps the starting gold in uox.ini and gives no food: the gold first, bread and water last.
    (common,) = [entry_set for entry_set in run.sets if entry_set["common"]]
    assert [entry["items"] for entry in common["items"]] == [
        ["0x0eed_gold_coin"],
        ["0x0f7a_black_pearl"],
        ["0x103b_bread_loaf"],
        ["0x1f9e_pitcher_of_water"],
    ]
    assert [entry.get("amount") for entry in common["items"]] == [1000, None, 3, None]
    assert all(entry["equip"] is False for entry in common["items"])
    (other,) = [entry_set for entry_set in run.sets if not entry_set["common"]]
    assert len(other["items"]) == 1


def test_with_no_default_all_section_the_common_set_is_made_for_moongates_own_items(uox_workspace):
    run = Run(uox_workspace, "[BESTSKILL 0]\n{\nPACKITEM=0x0f7a\n}\n", "[0x0eed]\n{\nid=0x0eed\nname=gold coin\n}\n")

    assert run.code == 0, uox_workspace.combined

    (common,) = [entry_set for entry_set in run.sets if entry_set["common"]]
    assert [entry["items"] for entry in common["items"]] == [["0x0eed_gold_coin"]]


def test_the_header_is_followed_by_a_blank_line(uox_workspace):
    run = Run(uox_workspace, "[DEFAULT ALL]\n{\nPACKITEM=0x0f7a\n}\n")

    assert run.code == 0
    assert "====\n\n[[set]]" in run.text


def test_the_file_is_written_as_tomlyn_writes_it(uox_workspace):
    run = Run(uox_workspace, "[BESTSKILL 0]\n{\nPACKITEM=0x0f7a,3,1\nEQUIPITEM=0x1f03,0x4ca,0\n}\n[DEFAULT ELF FEMALE]\n{\nEQUIPITEM=0x1f03\n}\n")

    assert run.code == 0, uox_workspace.combined
    assert run.text.split("====\n\n", 1)[1] == (
        '[[set]]\ncommon = false\nskill = "alchemy"\n'
        '[[set.items]]\nitems = ["0x0f7a_black_pearl"]\namount = 3\nequip = false\nnewbie = true\n[set.items.book_values]\n\n'
        '[[set.items]]\nitems = ["0x1f03_robe"]\nhue = 1226\nequip = true\nnewbie = false\n[set.items.book_values]\n\n'
        '[[set]]\ncommon = false\nrace = "elf"\ngender = "female"\n'
        '[[set.items]]\nitems = ["0x1f03_robe"]\nequip = true\n[set.items.book_values]\n'
    )


def test_no_set_at_all_is_an_empty_array(uox_workspace):
    run = Run(uox_workspace, "")

    assert run.code == 0
    assert run.text.endswith("====\n\nset = []\n")
    assert "Converted 0 starting item set(s)" in uox_workspace.output.getvalue()


def test_the_run_reports_what_it_verified(uox_workspace):
    run = Run(uox_workspace, "[DEFAULT ALL]\n{\nPACKITEM=0x0f7a\n}\n")

    assert run.code == 0
    assert "Verified 1 starting item set(s) read back from disk: every item resolves." in uox_workspace.output.getvalue()


def test_a_hue_that_is_no_hue_is_refused(uox_workspace):
    uox_workspace.write_source("items.dfn", ITEMS)
    path = uox_workspace.mobile_source / "newbie" / "newbie.dfn"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("[DEFAULT ALL]\n{\nEQUIPITEM=0x1f03,0x1ffff\n}\n", encoding="utf-8")

    with pytest.raises(ValueError, match="is not a hue"):
        uox_starting_items.run(
            uox_workspace.mobile_source,
            uox_workspace.starting_items_destination,
            item_index(uox_workspace.source),
            uox_workspace.output,
            uox_workspace.error,
        )


def test_a_missing_newbie_file_exits_2_and_writes_nothing(uox_workspace):
    run = Run(uox_workspace, None)

    assert run.code == 2
    assert "Starting items source does not exist" in uox_workspace.error.getvalue()
    assert not run.destination.exists()


def test_starting_items_without_a_mobile_source_are_rejected(uox_workspace):
    uox_workspace.write_source("items.dfn", "[0x0f7a]\n{\nid=0x0f7a\n}\n")

    code = uox.run(
        uox_workspace.source,
        uox_workspace.destination,
        None,
        uox_workspace.output,
        uox_workspace.error,
        starting_items_destination=uox_workspace.starting_items_destination,
    )

    assert code == 2
    assert "--starting-items-destination needs --mobile-source" in uox_workspace.error.getvalue()
