"""The ``uox`` command: its options and their rules, the hand-over to the mobile passes, and the TOML it writes."""

from __future__ import annotations

import io
from decimal import Decimal
from pathlib import Path

import pytest

from conftest import read_toml
from moongate_convert import items, uox, uox_mobiles, uox_spawns, uox_starting_items
from moongate_convert.cli import main
from moongate_convert.specs import HueSpec, RangeValue


def run_with(workspace, **options) -> int:
    return uox.run(workspace.source, workspace.destination, None, workspace.output, workspace.error, **options)


def test_bad_options_or_source_exit_with_2(uox_workspace):
    assert run_with(uox_workspace, mobile_source=Path("npc")) == 2
    assert "go together" in uox_workspace.error.getvalue()

    assert uox.run(uox_workspace.source / "missing", uox_workspace.destination, None, uox_workspace.output, uox_workspace.error) == 2
    assert "Source does not exist" in uox_workspace.error.getvalue()

    assert run_with(uox_workspace) == 2
    assert "No .dfn files" in uox_workspace.error.getvalue()


@pytest.mark.parametrize(
    ("options", "message"),
    [
        ({"mobile_source": Path("a"), "mobile_destination": Path("b")}, "--mobile-source, --mobile-destination and --names-destination go together"),
        ({"names_destination": Path("n")}, "--mobile-source, --mobile-destination and --names-destination go together"),
        ({"starting_items_destination": Path("s")}, "--starting-items-destination needs --mobile-source"),
        ({"npc_lists_destination": Path("n")}, "--npc-lists-destination and --spawns-destination go together"),
        ({"spawns_destination": Path("s")}, "--npc-lists-destination and --spawns-destination go together"),
        (
            {"npc_lists_destination": Path("n"), "spawns_destination": Path("s")},
            "--npc-lists-destination and --spawns-destination need --mobile-source",
        ),
    ],
)
def test_the_option_rules_are_checked_before_anything_is_read(uox_workspace, options, message):
    assert run_with(uox_workspace, **options) == 2
    assert message in uox_workspace.error.getvalue()
    assert not uox_workspace.destination.exists()


def test_a_single_dfn_file_is_a_source_and_its_name_is_the_output_name(uox_workspace):
    file = uox_workspace.write_source("one/torch.dfn", "[base_torch]\n{\nid=0x0f6b\n}\n")

    assert uox.run(file, uox_workspace.destination, None, uox_workspace.output, uox_workspace.error) == 0

    assert [item["id"] for item in read_toml(uox_workspace.destination / "torch.toml")["item"]] == ["base_torch"]
    assert "torch.dfn -> torch.toml (1 item(s))" in uox_workspace.output.getvalue()


def test_the_report_names_each_file_and_counts_what_was_skipped(uox_workspace):
    uox_workspace.write_source("a.dfn", "[base_torch]\n{\nid=0x0f6b\n}\n[nothing]\n{\nname=x\n}\n[LOOTLIST l]\n{\n10|base_torch\n}\n")
    uox_workspace.write_source("b.dfn", "[base_torch]\n{\nid=0x0f6c\n}\n")

    assert uox_workspace.run() == 0

    output = uox_workspace.output.getvalue()
    # UOX3 keeps the later definition of a header: the base_torch of a.dfn is the duplicate.
    assert "a.dfn -> a.toml" not in output
    assert "a.dfn -> 1 loot table(s), one file each under --loot-destination" in output
    assert "b.dfn -> b.toml (1 item(s))" in output
    assert (
        "Converted 1 item(s) and 1 loot table(s); skipped 1 block(s) with no id= of their own, 1 duplicate of an already-converted header, "
        "and 0 loot entry/entries pointing at nothing this converter could resolve." in output
    )
    assert "Duplicate block '[base_torch]'" in uox_workspace.error.getvalue()


def test_an_item_file_that_ends_up_with_no_item_is_not_written(uox_workspace):
    uox_workspace.write_source("a.dfn", "[nothing]\n{\nname=x\n}\n")

    assert uox_workspace.run() == 0

    assert not (uox_workspace.destination / "a.toml").exists()
    assert "Verified 0 item(s) and 0 loot table(s)" in uox_workspace.output.getvalue()


def test_the_mobile_passes_are_handed_the_item_index_in_order(uox_workspace, monkeypatch):
    uox_workspace.write_source("items.dfn", "[base_torch]\n{\nid=0x0f6b\nname=torch\n}\n[LOOTLIST gems]\n{\n10|blank\n}\n")
    calls: list[tuple] = []

    def record(name, result=0):
        def run(*arguments):
            calls.append((name, arguments))

            return result

        return run

    monkeypatch.setattr(uox_mobiles, "run", record("mobiles"))
    monkeypatch.setattr(uox_starting_items, "run", record("starting"))
    monkeypatch.setattr(uox_spawns, "run", record("spawns"))

    code = uox.run(
        uox_workspace.source,
        uox_workspace.destination,
        uox_workspace.loot_destination,
        uox_workspace.output,
        uox_workspace.error,
        mobile_source=uox_workspace.mobile_source,
        mobile_destination=uox_workspace.mobile_destination,
        names_destination=uox_workspace.names_destination,
        starting_items_destination=uox_workspace.starting_items_destination,
        npc_lists_destination=uox_workspace.npc_lists_destination,
        spawns_destination=uox_workspace.spawns_destination,
    )

    assert code == 0
    assert [name for name, _ in calls] == ["mobiles", "starting", "spawns"]
    (_, mobiles), (_, starting), (_, spawns) = calls
    assert mobiles[:3] == (uox_workspace.mobile_source, uox_workspace.mobile_destination, uox_workspace.names_destination)
    index = mobiles[3]
    assert index.item_id_by_header["BASE_TORCH"] == "base_torch"
    assert index.item_blocks_by_header["base_torch"].header == "base_torch"
    assert "GEMS" in index.loot_ids
    assert index.item_ids == {"base_torch"}
    assert mobiles[4:] == (uox_workspace.output, uox_workspace.error)
    assert starting[:2] == (uox_workspace.mobile_source, uox_workspace.starting_items_destination) and starting[2] is index
    assert spawns[:4] == (
        uox_workspace.mobile_source,
        uox_workspace.mobile_destination,
        uox_workspace.npc_lists_destination,
        uox_workspace.spawns_destination,
    )


def test_a_failing_mobile_pass_ends_the_run_with_its_exit_code(uox_workspace, monkeypatch):
    uox_workspace.write_source("items.dfn", "[base_torch]\n{\nid=0x0f6b\n}\n")
    called: list[str] = []
    monkeypatch.setattr(uox_mobiles, "run", lambda *arguments: 2)
    monkeypatch.setattr(uox_starting_items, "run", lambda *arguments: called.append("starting") or 0)

    code = run_with(
        uox_workspace,
        mobile_source=uox_workspace.mobile_source,
        mobile_destination=uox_workspace.mobile_destination,
        names_destination=uox_workspace.names_destination,
        starting_items_destination=uox_workspace.starting_items_destination,
    )

    assert code == 2
    assert called == []


def test_the_command_line_has_the_flags_of_the_uox_command(tmp_path):
    source = tmp_path / "source"
    source.mkdir()
    (source / "items.dfn").write_text("[base_torch]\n{\nid=0x0f6b\n}\n[LOOTLIST gems]\n{\n10|blank\n}\n", encoding="utf-8")
    output, error = io.StringIO(), io.StringIO()

    code = main(
        ["uox", "--source", str(source), "--destination", str(tmp_path / "items"), "--loot-destination", str(tmp_path / "loots")],
        output,
        error,
    )

    assert code == 0, error.getvalue()
    assert (tmp_path / "items" / "items.toml").is_file()
    assert (tmp_path / "loots" / "gems.toml").is_file()


def test_an_unreadable_existing_toml_in_the_destination_is_a_bad_source_not_a_traceback(uox_workspace):
    uox_workspace.write_source("items.dfn", "[base_torch]\n{\nid=0x0f6b\n}\n")
    uox_workspace.destination.mkdir()
    (uox_workspace.destination / "old.toml").write_text("this is = = not toml", encoding="utf-8")

    assert uox_workspace.run() == 2
    assert "UOX3 conversion failed" in uox_workspace.error.getvalue()


# --- the TOML text ---


def test_an_item_is_written_with_its_keys_in_the_order_of_the_server_and_the_tags_last():
    item = items.ItemTemplate(
        id="axe",
        item_id=5,
        base_id="base_item",
        name='a "good" axe',
        script_id="food",
        movable=True,
        weight=Decimal(700) / Decimal(100),
        amount=RangeValue.from_range(1, 3),
        layer="two_handed",
        buy_price=10,
        loot_type="newbied",
        tags={"Level": "7", "odd key": "x"},
        visibility="game_master",
        hue=HueSpec.from_range(0x0481, 0x0489),
        max_weight=4,
    )

    assert items.serialize_items([item, items.ItemTemplate(id="b", item_id=0, weight=Decimal(2) / Decimal(100))]) == (
        '[[item]]\nid = "axe"\nbase_id = "base_item"\nitem_id = 5\nname = "a \\"good\\" axe"\nrarity = "common"\nscript_id = "food"\n'
        'movable = true\nweight = 7.0\namount = "1-3"\nlayer = "two_handed"\nbuy_price = 10\nloot_type = "newbied"\n'
        'visibility = "game_master"\nhue = "0x0481-0x0489"\nmax_weight = 4\n[item.tags]\nLevel = "7"\n"odd key" = "x"\n'
        '\n[[item]]\nid = "b"\nitem_id = 0\nrarity = "common"\nweight = 0.02\n'
    )


@pytest.mark.parametrize(("hundredths", "text"), [(0, "0.0"), (100, "1.0"), (255, "2.55"), (10, "0.1"), (-5, "-0.05"), (1000000, "10000.0")])
def test_a_weight_is_a_decimal_with_at_least_one_decimal_place(hundredths, text):
    assert items._decimal(Decimal(hundredths) / Decimal(100)) == text


def test_an_empty_loot_table_and_a_nested_one_are_written_as_the_server_writes_them():
    assert items.serialize_loot(items.LootTemplate("none")) == '[[loot]]\nid = "none"\nentries = []\n'
    assert items.serialize_loot(
        items.LootTemplate(
            "gems",
            [items.LootEntry(weight=80), items.LootEntry(weight=20, item_id="gem", comment="a gem", amount=RangeValue.from_range(1, 2))],
        )
    ) == (
        '[[loot]]\nid = "gems"\n[[loot.entries]]\nweight = 80\namount = 1\n\n'
        '[[loot.entries]]\nweight = 20\nitem_id = "gem"\ncomment = "a gem"\namount = "1-2"\n'
    )


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("0x0481", (0x0481, 0x0481, False)),
        ("1153", (1153, 1153, False)),
        ("0x0481-0x0489", (0x0481, 0x0489, True)),
        ("hue(1153:1161)", (1153, 1161, True)),
        (" 0X44E ", (0x44E, 0x44E, False)),
    ],
)
def test_a_hue_spec_reads_the_forms_of_the_server(text, expected):
    spec = HueSpec.try_parse(text)

    assert spec is not None and (spec.min, spec.max, spec.is_range) == expected


@pytest.mark.parametrize("text", [None, "", "  ", "0x10000", "-5", "0x489-0x481", "hue(1:2:3)", "red", "1-2-3"])
def test_a_hue_spec_refuses_anything_else(text):
    assert HueSpec.try_parse(text) is None
