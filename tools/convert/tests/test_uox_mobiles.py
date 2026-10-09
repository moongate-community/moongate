"""The mobile pass of the ``uox`` command: UOX3's NPCs, creatures and name lists as mobile templates and ``names.toml``.

The tests of ``UoxMobileConverterTests`` of the C# converter, ported. A second set, with ``MOONGATE_MGCTL`` set to the path of a built
``mgctl``, runs the C# converter on the same sources and requires the same files, report and warnings.
"""

from __future__ import annotations

import io
import os
import subprocess
import tomllib
from pathlib import Path

import pytest

from moongate_convert import uox
from moongate_convert.specs import DiceSpec

MGCTL = os.environ.get("MOONGATE_MGCTL")
ITEMS = "[0x0eed]\n{\nid=0x0eed\n}\n"
NAMES = "[RANDOMNAME 1]\n{\nAaron\n}\n[RANDOMNAME 2]\n{\nAba\n}\n"


class Workspace:
    """The folders of one run: the item source, ``dfndata`` (the mobile source) with its sibling ``dictionaries`` and the destinations."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.source = root / "source"
        self.destination = root / "destination"
        self.loot_destination = root / "loot-destination"
        self.mobile_source = root / "dfndata"
        self.mobile_destination = root / "mobile-destination"
        self.names_destination = root / "names" / "names.toml"
        self.source.mkdir(parents=True)
        self.output = io.StringIO()
        self.error = io.StringIO()

    @property
    def combined(self) -> str:
        return self.output.getvalue() + self.error.getvalue()

    def items(self, content: str = ITEMS) -> None:
        self.write(self.source / "items.dfn", content)

    def names(self, content: str = NAMES) -> None:
        self.mobile("npc/namelists.dfn", content)

    def mobile(self, relative: str, content: str) -> None:
        self.write((self.mobile_source / relative).resolve(), content)

    def items_and_names(self) -> None:
        self.items()
        self.names()

    @staticmethod
    def write(path: Path, content: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def run(self) -> int:
        return uox.run(
            self.source,
            self.destination,
            self.loot_destination,
            self.output,
            self.error,
            mobile_source=self.mobile_source,
            mobile_destination=self.mobile_destination,
            names_destination=self.names_destination,
        )

    def mobiles(self, file: str) -> dict[str, dict]:
        document = tomllib.loads((self.mobile_destination / file).read_text(encoding="utf-8"))

        return {mobile["id"]: mobile for mobile in document["mobile"]}


@pytest.fixture
def workspace(tmp_path: Path) -> Workspace:
    return Workspace(tmp_path / "uox")


def test_the_name_lists_are_written_with_readable_ids_and_dictionary_names(workspace):
    workspace.items()
    workspace.mobile(
        "npc/namelists.dfn",
        "[RANDOMNAME 1]\n{ Basic Human Male Names\nAaron\nAbbott\nAaron\n}\n[RANDOMNAME 5]\n{\n3009//a daemon\nImp\n}\n",
    )
    workspace.mobile("../dictionaries/dictionary.ENG", "3009=a daemon\n")

    assert workspace.run() == 0, workspace.combined

    lists = {entry["id"]: entry["names"] for entry in tomllib.loads(workspace.names_destination.read_text())["names"]}

    assert lists["male"] == ["Aaron", "Abbott"]
    assert lists["daemon"] == ["a daemon", "Imp"]


def test_the_names_header_is_followed_by_a_blank_line(workspace):
    workspace.items_and_names()

    assert workspace.run() == 0, workspace.combined
    assert "====\n\n[[names]]" in workspace.names_destination.read_text()


def test_inheritance_follows_get_and_the_lbr_era(workspace):
    workspace.items_and_names()
    workspace.mobile(
        "npc/orcs.dfn",
        "[base_orc]\n{\nNAME=#//an orc\nID=0x0011\n}\n[orc_lbr]\n{\nGET=base_orc\nTITLE=5052//the Blacksmith\n}\n"
        "[orc_aos]\n{\nGET=base_orc\n}\n[orc]\n{\nGETAOS=orc_aos\nGETLBR=orc_lbr\n}\n",
    )
    workspace.mobile("../dictionaries/dictionary.ENG", "5052=the Blacksmith\n")

    assert workspace.run() == 0, workspace.combined

    mobiles = workspace.mobiles("orcs.toml")

    assert (mobiles["base_orc"]["name"], mobiles["base_orc"]["body"]) == ("an orc", 0x11)
    assert mobiles["orc_lbr"]["base_id"] == "base_orc"
    assert mobiles["orc_lbr"]["title"] == "the Blacksmith"
    assert mobiles["orc"]["base_id"] == "orc_lbr"


@pytest.mark.parametrize(
    ("lines", "race", "gender", "body", "name_list"),
    [
        ("ID=0x0190", "human", "male", None, "male"),
        ("ID=0x0191", "human", "female", None, "female"),
        ("ID=0x025E", "elf", "female", None, "female"),
        ("ID=0x0033\nRACE=22", None, None, 0x33, "female"),
    ],
)
def test_humanoid_bodies_become_race_and_gender(workspace, lines, race, gender, body, name_list):
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", f"[x]\n{{\n{lines}\nNAMELIST=2\n}}\n")

    assert workspace.run() == 0, workspace.combined

    x = workspace.mobiles("a.toml")["x"]

    # NAMELIST=2 (female) follows the gender on a male body.
    assert (x.get("race"), x.get("gender"), x.get("body"), x["name_list"]) == (race, gender, body, name_list)


def test_a_doubled_hex_prefix_is_read_as_one(workspace):
    # UOX3's base_raiju has ID=0x0xc7.
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", "[x]\n{\nID=0x0xc7\n}\n")

    assert workspace.run() == 0, workspace.combined
    assert workspace.mobiles("a.toml")["x"]["body"] == 0xC7


def test_the_mobiles_moongate_has_a_script_for_get_their_script_id(workspace):
    workspace.items_and_names()
    workspace.mobile(
        "npc/undead.dfn",
        "[skeleton]\n{\nID=0x0032\n}\n[zombie]\n{\nID=0x0003\n}\n[wraith]\n{\nID=0x001a\n}\n[ghoul]\n{\nGET=wraith\n}\n"
        "[spectre]\n{\nID=0x001a\n}\n[lich]\n{\nID=0x0018\n}\n[headless]\n{\nID=0x001f\n}\n[boneknight]\n{\nGET=skeleton\n}\n"
        "[m_banker]\n{\nID=0x0190\nNPCAI=8\n}\n[orc]\n{\nID=0x0011\nNPCAI=2\n}\n[m_guard]\n{\nID=0x0190\nNPCAI=4\n}\n"
        "[fighter]\n{\nID=0x0190\nNPCAI=5\n}\n[bunny]\n{\nID=0x00cd\nNPCAI=6\n}\n[mage]\n{\nID=0x0190\nNPCAI=10\n}\n"
        "[evilmage]\n{\nID=0x0190\nNPCAI=11\n}\n[rabbit]\n{\nID=0x00cd\nNPCAI=12\n}\n[chaos]\n{\nID=0x0190\nNPCAI=88\n}\n"
        "[merchant]\n{\nID=0x0190\nNPCAI=7\n}\n[orcking]\n{\nGET=orc\n}\n",
    )

    assert workspace.run() == 0, workspace.combined

    mobiles = workspace.mobiles("undead.toml")
    script = {name: mobile.get("script_id") for name, mobile in mobiles.items()}

    assert (script["skeleton"], script["zombie"], script["m_banker"]) == ("monster", "monster", "banker")
    assert all(script[name] == "monster" for name in ("wraith", "ghoul", "spectre", "lich", "headless"))
    # UOX3's AI of the town guards.
    assert script["m_guard"] == "guard"
    # A template based on one of them takes the script through its base; the others have none yet.
    assert script["boneknight"] is None
    # The creatures that go for the players, the animals that keep to themselves and those that run.
    assert all(script[name] == "monster" for name in ("orc", "evilmage", "chaos"))
    # The good fighters and casters fight criminals only: no script yet.
    assert script["fighter"] is None and script["mage"] is None
    assert (script["bunny"], script["rabbit"]) == ("animal", "scared_animal")
    # A dummy, 7, has none yet; one based on a monster takes its script through its base.
    assert script["merchant"] is None and script["orcking"] is None


def test_flee_at_is_the_hit_percent_under_which_the_creature_runs_minus_one_for_never(workspace):
    workspace.items_and_names()
    workspace.mobile(
        "npc/undead.dfn",
        "[zombie]\n{\nID=0x0003\nFLEEAT=-1\n}\n[man]\n{\nID=0x0190\nFLEEAT=20\n}\n[calm]\n{\nID=0x0190\n}\n"
        "[bad]\n{\nID=0x0190\nFLEEAT=500\n}\n[zero]\n{\nID=0x0190\nFLEEAT=0\n}\n",
    )

    assert workspace.run() == 0, workspace.combined

    mobiles = workspace.mobiles("undead.toml")

    assert (mobiles["zombie"].get("flee_at"), mobiles["man"].get("flee_at"), mobiles["calm"].get("flee_at")) == (-1, 20, None)
    # A value out of 0 to 100 is left out rather than written as a template that cannot load.
    assert "flee_at" not in mobiles["bad"]
    # 0 is UOX3's "the default of the server": nothing is written.
    assert "flee_at" not in mobiles["zero"]


def test_a_random_pick_of_two_creatures_becomes_the_first(workspace):
    # UOX3's [dragon] GET=graydragon reddragon: a template has one base, so the first is kept and counted.
    workspace.items_and_names()
    workspace.mobile(
        "npc/dragons.dfn", "[graydragon]\n{\nID=0x000c\n}\n[reddragon]\n{\nID=0x003b\n}\n[dragon]\n{\nGET=graydragon reddragon\n}\n"
    )

    assert workspace.run() == 0, workspace.combined
    assert workspace.mobiles("dragons.toml")["dragon"]["base_id"] == "graydragon"
    assert "1 x two-target get, first target kept" in workspace.output.getvalue()


def test_an_f_prefixed_npc_with_a_male_body_is_female_so_its_pair_merges(workspace):
    # UOX3's femalevendors.dfn gives [f_scribe] the male body 0x0190.
    workspace.items_and_names()
    workspace.mobile(
        "npc/vendors.dfn",
        "[m_scribe]\n{\nID=0x0190\nNAMELIST=1\n}\n[f_scribe]\n{\nID=0x0190\nNAMELIST=2\n}\n[scribe]\n{\nGET=m_scribe f_scribe\n}\n",
    )

    assert workspace.run() == 0, workspace.combined

    mobiles = workspace.mobiles("vendors.toml")

    assert mobiles["f_scribe"]["gender"] == "female"
    assert (mobiles["scribe"]["gender"], mobiles["scribe"]["name_list"]) == ("random", "{gender}")


def test_a_race_on_a_non_human_body_is_dropped(workspace):
    # UOX3's [giantrat] has RACE=2 (gargoyle) on the rat body.
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", "[giantrat]\n{\nID=0x00d7\nRACE=2\n}\n")

    assert workspace.run() == 0, workspace.combined

    rat = workspace.mobiles("a.toml")["giantrat"]

    assert (rat["body"], rat.get("race")) == (0xD7, None)


@pytest.mark.parametrize(("lines", "name_list"), [("ID=0x0191\nNAMELIST=1", "female"), ("ID=0x0190\nNAMELIST=2", "male")])
def test_a_male_or_female_name_list_follows_the_gender(workspace, lines, name_list):
    # UOX3's [f_paladin] and others use the male list on a female body.
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", f"[x]\n{{\n{lines}\n}}\n")

    assert workspace.run() == 0, workspace.combined
    assert workspace.mobiles("a.toml")["x"]["name_list"] == name_list


def test_numbers_become_dice(workspace):
    workspace.items_and_names()
    workspace.mobile(
        "npc/a.dfn",
        "[x]\n{\nID=0x0011\nSTR=96 120\nDEX=50\nHPMAX=58 72\nHP=1\nDAMAGE=3 9\nDEF=14\nRESISTFIRE=20 30\nELEMENTRESIST=10 11 12 13\n"
        "MAGERY=500 700\nMAGICRESISTANCE=655\nSWORDSMANSHIP=1000 1500\nSWORDFIGHTING=500\nKARMA=-2500\nFAME=2500\nGOLD=0 50\n"
        "FLAG=NEUTRAL\nCUSTOMINTTAG=Level 7\n}\n",
    )

    assert workspace.run() == 0, workspace.combined

    x = workspace.mobiles("a.toml")["x"]

    assert x["strength"] == "1d25+95"
    assert x["dexterity"] == 50
    assert x["hits"] == "1d15+57"
    assert x["damage"] == "1d7+2"
    assert x["armor"] == 14
    assert x["resistances"] == {"fire": 10, "cold": 11, "poison": 13, "energy": 12}
    assert x["skills"]["magery"] == "1d21+49"
    assert x["skills"]["resisting_spells"] == 65
    assert DiceSpec.try_parse(str(x["skills"]["swordsmanship"])).max == 120
    assert len(x["skills"]) == 3
    assert (x["karma"], x["fame"]) == (-2500, 2500)
    assert x["gold"] == "1d51-1"
    assert x["notoriety"] == "attackable"
    assert x["tags"]["Level"] == "7"


def test_equipment_colours_and_loot_resolve_against_the_items(workspace):
    workspace.items(
        "[0x13e4]\n{\nid=0x13e4\n}\n[0x1517]\n{\nid=0x1517\n}\n[ITEMLIST 20]\n{\n0x1517\n50|blank\n10|0x13e4\n}\n"
        "[ITEMLIST 13]\n{\n0x203b\n}\n[LOOTLIST orcLoot]\n{\n0x13e4\n}\n"
    )
    workspace.names()
    workspace.mobile("colors/colors.dfn", "[RANDOMCOLOR 11]\n{\n0x0835\n0x0836\n}\n[RANDOMCOLOR 33]\n{\n0x0003\n0x0059\n}\n")
    workspace.mobile(
        "npc/a.dfn",
        "[x]\n{\nCOLOR=0x0010\nID=0x0190\nEQUIPITEM=listobject13\nEQUIPITEM=0x13e4\nCOLOR=0x0455\nEQUIPITEM=listobject20\nCOLORLIST=11\n"
        "EQUIPITEM=0x1f13\nEQUIPITEM=0x13e4\nCOLORLIST=33\nLOOT=orcLoot,2\nLOOT=nothing\n}\n",
    )

    assert workspace.run() == 0, workspace.combined

    x = workspace.mobiles("a.toml")["x"]
    equipment = x["equipment"]

    assert len(equipment) == 3
    assert equipment[0]["items"] == ["0x13e4"]
    assert equipment[0]["hue"] == 0x455
    assert equipment[1]["items"] == ["0x1517", "0x13e4"]
    assert equipment[1]["hue"] == "0x0835-0x0836"
    assert "hue" not in equipment[2]
    assert x["loot"] == ["orc_loot", "orc_loot"]

    for reason in ("unresolved item", "unresolved loot", "colour list not a range", "item list weight or blank dropped"):
        assert reason in workspace.combined


def test_sounds_come_from_creatures_on_the_block_that_sets_the_body(workspace):
    workspace.items_and_names()
    workspace.mobile(
        "creatures/creatures.dfn",
        "[CREATURE 0x11]\n{ Orc\nSOUND_STARTATTACK=0x1b0\nSOUND_IDLE=0x1b1\nSOUND_ATTACK=0x1b2\nSOUND_DEFEND=0x1b3\nSOUND_DIE=0x1b4\n}\n"
        "[CREATURE 0x190]\n{ Human Male\nSOUND_DIE=0x15c\n}\n",
    )
    workspace.mobile("npc/a.dfn", "[base_orc]\n{\nID=0x0011\n}\n[orc]\n{\nGET=base_orc\n}\n[man]\n{\nID=0x0190\n}\n")

    assert workspace.run() == 0, workspace.combined

    mobiles = workspace.mobiles("a.toml")

    assert mobiles["base_orc"]["sounds"] == {"start_attack": 0x1B0, "idle": 0x1B1, "attack": 0x1B2, "hurt": 0x1B3, "death": 0x1B4}
    assert "sounds" not in mobiles["orc"]
    assert mobiles["man"]["sounds"] == {"death": 0x15C}


def test_movement_comes_from_creatures_on_the_block_that_sets_the_body_and_a_land_body_overrides_a_water_base(workspace):
    workspace.items_and_names()
    workspace.mobile(
        "creatures/creatures.dfn",
        "[CREATURE 0x97]\n{ Dolphin\nMOVEMENT=WATER\n}\n[CREATURE 0xdd]\n{ Walrus\nMOVEMENT=BOTH\n}\n[CREATURE 0x11]\n{ Orc\nMOVEMENT=LAND\n}\n",
    )
    workspace.mobile(
        "npc/a.dfn",
        "[dolphin]\n{\nID=0x0097\n}\n[big_dolphin]\n{\nGET=dolphin\n}\n[walrus]\n{\nID=0x00dd\n}\n[orc]\n{\nID=0x0011\n}\n"
        "[beached]\n{\nGET=dolphin\nID=0x0011\n}\n",
    )

    assert workspace.run() == 0, workspace.combined

    mobiles = workspace.mobiles("a.toml")

    assert [mobiles[name].get("movement") for name in ("dolphin", "big_dolphin", "walrus", "orc", "beached")] == [
        "water", None, "both", None, "land",
    ]  # fmt: skip


def test_a_male_female_pair_becomes_one_random_gender_template_and_other_pairs_are_skipped(workspace):
    workspace.items("[0x13e4]\n{\nid=0x13e4\n}\n[0x1517]\n{\nid=0x1517\n}\n[0x1516]\n{\nid=0x1516\n}\n")
    workspace.names()
    workspace.mobile(
        "npc/a.dfn",
        "[basehuman]\n{\nFLAG=INNOCENT\n}\n[m_guard]\n{\nGET=basehuman\nNAMELIST=1\nID=0x0190\nDEF=20\nEQUIPITEM=0x13e4\nEQUIPITEM=0x1517\n}\n"
        "[f_guard]\n{\nGET=basehuman\nNAMELIST=2\nID=0x0191\nDEF=100\nEQUIPITEM=0x13e4\nEQUIPITEM=0x1516\n}\n"
        "[guard]\n{\nGET=m_guard f_guard\n}\n[dragon]\n{\nGET=m_guard basehuman\n}\n",
    )

    assert workspace.run() == 0, workspace.combined

    mobiles = workspace.mobiles("a.toml")
    guard = mobiles["guard"]

    assert (guard["gender"], guard["race"], guard["name_list"], guard["base_id"]) == ("random", "human", "{gender}", "basehuman")
    assert guard["armor"] == 20
    assert [(entry["items"], entry.get("gender")) for entry in guard["equipment"]] == [
        (["0x13e4"], None), (["0x1517"], "male"), (["0x1516"], "female"),
    ]  # fmt: skip
    assert "m_guard" in mobiles
    # Not a gender pair: the first target is kept as its base.
    assert mobiles["dragon"]["base_id"] == "m_guard"
    assert "differs between the male and female" in workspace.combined
    assert "two-target get, first target kept" in workspace.combined


def test_the_written_mobiles_are_read_back_and_verified(workspace):
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", "[x]\n{\nID=0x0011\nSTR=10\n}\n")

    assert workspace.run() == 0, workspace.combined
    assert "Verified 1 mobile(s) and 2 name list(s)" in workspace.combined


def test_a_template_that_fails_validation_fails_the_run(workspace):
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", "[x]\n{\nID=0x0011\nDEF=-5\n}\n")

    assert workspace.run() == 1
    assert "Mobile template 'x': armor" in workspace.combined


def test_a_pair_with_different_sounds_leaves_the_sounds_unset(workspace):
    workspace.items_and_names()
    workspace.mobile("creatures/creatures.dfn", "[CREATURE 0x190]\n{\nSOUND_DIE=0x15c\n}\n[CREATURE 0x191]\n{\nSOUND_DIE=0x151\n}\n")
    workspace.mobile(
        "npc/a.dfn", "[m_guard]\n{\nID=0x0190\n}\n[f_guard]\n{\nID=0x0191\n}\n[guard]\n{\nGET=m_guard f_guard\n}\n"
    )

    assert workspace.run() == 0, workspace.combined
    assert "sounds" not in workspace.mobiles("a.toml")["guard"]


def test_equipment_through_an_item_alias_follows_its_era_or_random_get(workspace):
    workspace.items(
        "[0x170b]\n{\nid=0x170b\n}\n[0x170c]\n{\nid=0x170c\n}\n[boots]\n{\nget=0x170b 0x170c\n}\n"
        "[0x13bb_lbr]\n{\nid=0x13bb\n}\n[0x13bb]\n{\ngetlbr=0x13bb_lbr\n}\n"
    )
    workspace.names()
    workspace.mobile("npc/a.dfn", "[x]\n{\nID=0x0011\nEQUIPITEM=boots\nEQUIPITEM=0x13bb\n}\n")

    assert workspace.run() == 0, workspace.combined

    equipment = workspace.mobiles("a.toml")["x"]["equipment"]

    assert equipment[0]["items"] == ["0x170b", "0x170c"]
    # [0x13bb] getlbr=0x13bb_lbr has one parent, so it is a template of its own inheriting that graphic.
    assert equipment[1]["items"] == ["0x13bb"]


def test_two_headers_that_make_one_id_are_a_bad_source_where_the_csharp_throws(workspace):
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", "[g-h]\n{\nID=0x0011\n}\n[g_h]\n{\nID=0x0012\n}\n")

    assert workspace.run() == 2
    assert "two npc headers become the mobile id 'g_h'" in workspace.error.getvalue()
    assert not workspace.mobile_destination.exists()


def test_a_range_too_wide_for_a_die_is_a_bad_number_where_the_csharp_throws(workspace):
    workspace.items_and_names()
    workspace.mobile("npc/a.dfn", "[x]\nID=0x0011\n".replace("[x]\n", "[x]\n{\n") + "FAME=2147483647 -2147483647\n}\n")

    assert workspace.run() == 0, workspace.combined
    assert "1 x bad number" in workspace.output.getvalue()


# --- the C# converter as the reference ---

# Sources that reach the corners of the converter: each is run through both converters, which must write the same files and say the same.
CASES: dict[str, dict[str, str]] = {
    "no npc folder, no name lists": {"items.dfn": ITEMS},
    "name lists with blanks, duplicates and unknown dictionary ids": {
        "items.dfn": ITEMS,
        "dfndata/npc/namelists.dfn": "[RANDOMNAME 3]\n{\n  \n1\n2//two\n 3 \nOrc\nOrc\n}\n[RANDOMNAME 99]\n{\nx\n}\n"
        "[RANDOMNAME 4]\n{\n7//\n}\n[RANDOMNAME abc]\n{\nzed\n}\n[OTHER]\n{\nq\n}\n",
        "dictionaries/dictionary.ENG": "1=\n3=three\nx=1\n",
    },
    "numbers that are not numbers, ranges and limits": {
        "items.dfn": ITEMS,
        "dfndata/npc/a.dfn": "[x]\n{\nID=0x0011\nSTR=abc\nDEX=1 2 3\nINT=120 96\nHP=-5 5\nMANA=7\nMANAMAX=9\nSTAMINA=3\nSTAMINAMAX=\n"
        "GOLD=0x10\nKARMA=-1 -9\nFAME=2147483647 2147483640\nMAGERY=99999\nTACTICS=-50\nWRESTLING=15 25\nDAMAGE=5 5\nATT=2 4\n"
        "ELEMENTRESIST=1 2 3\nELEMENTRESIST=a b c d\nRESISTCOLD=x\nFLAG=evil\nFLAG=weird\nNPCAI= 4 \n}\n",
    },
    "tags, skin, colour lists and hair": {
        "items.dfn": ITEMS + "[ITEMLIST 13]\n{\n0x203b\n}\n[ITEMLIST 14]\n{\n0x203c\n}\n[0x1517]\n{\nid=0x1517\n}\n",
        "dfndata/colors/colors.dfn": "[RANDOMCOLOR 1]\n{\n0x0010\n0x0011\n0x0012\n}\n[RANDOMCOLOR 2]\n{\n0x0010\n0x0012\n}\n"
        "[RANDOMCOLOR 3]\n{\n0x0020\n}\n[RANDOMCOLOR 4]\n{\nred\n}\n",
        "dfndata/npc/a.dfn": "[x]\n{\nID=0x0011\nSKIN=0x0455\nSKINLIST=1\nCUSTOMINTTAG=Level 7\nCUSTOMSTRINGTAG=Say hello there\n"
        "CUSTOMINTTAG=Lonely\nCUSTOMINTTAG= 5\nCUSTOMINTTAG=Level 9\nEQUIPITEM=listobject14\nEQUIPITEM=0x1517\nCOLORLIST=3\nCOLOR=bad\n"
        "COLOUR=0x0033\nEQUIPITEM=0x1517\nCOLOURLIST=1\nCOLORLIST=77\n}\n[y]\n{\nID=0x0190\nSKIN=0x0455\nSKINLIST=1\nCOLOR=0x0010\n}\n"
        "[z]\n{\nSKINLIST=4\nSKINLIST=zz\nSKIN=9999999\n}\n",
    },
    "texts: dictionary ids, comments, quotes and accents": {
        "items.dfn": ITEMS,
        "dictionaries/dictionary.ENG": "100=the \"Great\" \\ one\n101=Città\n",
        "dfndata/npc/a.dfn": "[a]\n{\nNAME=100\nTITLE=101\n}\n[b]\n{\nNAME=#//a comment\nTITLE=#\n}\n[c]\n{\nNAME=999//unknown\nTITLE=999\n}\n"
        "[d]\n{\nNAME=Plain Name\nTITLE=the Wise\nNAMELIST=x\n}\n[e]\n{\nNAMELIST=77\n}\n[f]\n{\nNAMELIST=2\nID=0x0190\n}\n",
    },
    "inheritance corners": {
        "items.dfn": ITEMS,
        "dfndata/npc/a.dfn": "[a]\n{\nID=0x0011\n}\n[A]\n{\nID=0x0012\n}\n[b]\n{\nGET=missing\n}\n[c]\n{\nGET=a a2 a3\n}\n"
        "[d]\n{\nGET=a b\n}\n[e]\n{\nGETLBR=a\nGET=b\n}\n[f]\n{\nGET=NPCLIST 1\n}\n[NPCLIST 1]\n{\nx\n}\n"
        "[RANDOMNAME 1]\n{\nx\n}\n",
        "dfndata/npc/npclists/skip.dfn": "[should_not_be_read]\n{\nID=0x0011\n}\n",
        "dfndata/npc/sub/deep.dfn": "[deep]\n{\nGET=a\n}\n",
        "dfndata/npc/sub/empty.dfn": "// nothing\n",
    },
    "gender pairs that differ in everything": {
        "items.dfn": "[0x13e4]\n{\nid=0x13e4\n}\n[0x1517]\n{\nid=0x1517\n}\n",
        "dfndata/creatures/creatures.dfn": "[CREATURE 0x190]\n{\nSOUND_DIE=0x15c\nSOUND_IDLE=1\n}\n[CREATURE 0x191]\n{\nSOUND_DIE=0x151\nSOUND_IDLE=1\n}\n"
        "[CREATURE 0x25D]\n{\nSOUND_DIE=0x15c\n}\n[CREATURE 0x25E]\n{\nSOUND_DIE=0x15c\n}\n",
        "dfndata/npc/namelists.dfn": NAMES,
        "dfndata/npc/a.dfn": "[hum]\n{\nFLAG=INNOCENT\n}\n[hum2]\n{\nFLAG=EVIL\n}\n"
        "[m_a]\n{\nGET=hum\nNAMELIST=1\nID=0x0190\nTITLE=the man\nSTR=10\nDEX=5\nMAGERY=100\nTACTICS=200\nRESISTFIRE=10\nGOLD=5\n"
        "EQUIPITEM=0x13e4\nCOLOR=0x0010\nEQUIPITEM=0x1517\nLOOT=a\nCUSTOMINTTAG=Level 1\n}\n"
        "[f_a]\n{\nGET=hum2\nNAMELIST=2\nID=0x0191\nTITLE=the woman\nSTR=20\nINT=5\nTACTICS=200\nMAGERY=100\nRESISTFIRE=20\nGOLD=6\n"
        "EQUIPITEM=0x13e4\nCOLOR=0x0010\nEQUIPITEM=0x1517\nCOLOR=0x0011\nCUSTOMINTTAG=Level 2\n}\n[a]\n{\nGET=m_a f_a\n}\n"
        "[m_b]\n{\nID=0x025D\nNAMELIST=1\n}\n[f_b]\n{\nID=0x025E\nNAMELIST=1\n}\n[b]\n{\nGET=f_b m_b\n}\n"
        "[m_c]\n{\nID=0x0190\nNAMELIST=3\n}\n[f_c]\n{\nID=0x0191\nNAMELIST=4\n}\n[c]\n{\nGET=m_c f_c\n}\n"
        "[m_d]\n{\nID=0x0190\n}\n[f_d]\n{\nID=0x025E\n}\n[d]\n{\nGET=m_d f_d\n}\n[e]\n{\nGET=m_d f_missing\n}\n"
        "[m_e]\n{\nID=0x0190\n}\n[m_e2]\n{\nID=0x0190\n}\n[e2]\n{\nGET=m_e m_e2\n}\n",
    },
    "swimming through get chains": {
        "items.dfn": ITEMS,
        "dfndata/creatures/creatures.dfn": "[CREATURE 0x97]\n{\nMOVEMENT= water \nSOUND_ATTACK=3\n}\n[CREATURE 0xdd]\n{\nMOVEMENT=BOTH\n}\n"
        "[CREATURE zz]\n{\nMOVEMENT=BOTH\n}\n[CREATURE 0x11]\n{\nMOVEMENT=FLY\n}\n",
        "dfndata/npc/a.dfn": "[d]\n{\nID=0x0097\n}\n[d2]\n{\nGET=d\n}\n[d3]\n{\nGET=d2\nID=0x0011\n}\n[d4]\n{\nGET=d3\nID=0x0011\n}\n"
        "[d5]\n{\nGET=d2\nID=0x00dd\n}\n[loop1]\n{\nGET=loop2\n}\n[loop2]\n{\nGET=loop1\n}\n[loop3]\n{\nGET=loop3\nID=0x0011\n}\n",
    },
    "a mobile that fails validation, and several that do": {
        "items.dfn": ITEMS,
        "dfndata/npc/a.dfn": "[x]\n{\nID=0x0011\nDEF=-5\nSTR=-1 3\n}\n[y]\n{\nID=0x0011\nHP=-3\nMAGERY=-10\n}\n[z]\n{\nID=0x0011\nFAME=-4\n}\n",
    },
    "unresolved references everywhere": {
        "items.dfn": "[0x13e4]\n{\nid=0x13e4\n}\n[LOOTLIST known]\n{\n0x13e4\n}\n[ITEMLIST 5]\n{\n0x13e4\nblank\n3|0x13e4\n9|nothing\n}\n",
        "dfndata/npc/a.dfn": "[x]\n{\nID=0x0011\nEQUIPITEM=nothing\nEQUIPITEM=listobject5\nEQUIPITEM=listobject77\nEQUIPITEM=listobjectx\n"
        "LOOT=known\nLOOT=known,3\nLOOT=known,0\nLOOT=known,x\nLOOT=known,2 5\nLOOT=KNOWN\nLOOT=\nLOOT=missing,2\n}\n",
    },
    "a value out of range and a duplicate header": {
        "items.dfn": ITEMS,
        "dfndata/npc/a.dfn": "[x]\n{\nID=0x0011\nFLEEAT=-1\n}\n[X]\n{\nID=0x0012\n}\n[y]\n{\nFLEEAT=100\n}\n[z]\n{\nFLEEAT=101\n}\n"
        "[w]\n{\nFLEEAT=-2\n}\n",
        "dfndata/npc/b.dfn": "[x]\n{\nID=0x0013\n}\n",
    },
}


def _files(root: Path) -> dict[str, bytes]:
    return {path.relative_to(root).as_posix(): path.read_bytes() for path in sorted(root.rglob("*")) if path.is_file()}


def _write_case(root: Path, files: dict[str, str]) -> None:
    for relative, content in files.items():
        target = root / ("source" if relative == "items.dfn" else "") / ("items.dfn" if relative == "items.dfn" else relative)
        Workspace.write(target, content)


def _arguments(root: Path) -> list[str]:
    return [
        "--source", str(root / "source"),
        "--destination", str(root / "destination"),
        "--loot-destination", str(root / "loot-destination"),
        "--mobile-source", str(root / "dfndata"),
        "--mobile-destination", str(root / "mobile-destination"),
        "--names-destination", str(root / "names" / "names.toml"),
    ]  # fmt: skip


@pytest.mark.skipif(MGCTL is None, reason="MOONGATE_MGCTL is not set")
@pytest.mark.parametrize("case", list(CASES))
def test_the_csharp_converter_gives_the_same_files_report_and_warnings(tmp_path, case):
    csharp, python = tmp_path / "csharp", tmp_path / "python"

    for root in (csharp, python):
        (root / "source").mkdir(parents=True)
        _write_case(root, CASES[case])

    reference = subprocess.run([MGCTL or "", "convert", "uox", *_arguments(csharp)], capture_output=True, text=True, check=False)
    output, error = io.StringIO(), io.StringIO()

    from moongate_convert.cli import main

    code = main(["uox", *_arguments(python)], output, error)

    assert code == reference.returncode
    assert output.getvalue().replace(str(python), "<root>") == reference.stdout.replace(str(csharp), "<root>")
    assert error.getvalue().replace(str(python), "<root>") == reference.stderr.replace(str(csharp), "<root>")
    assert _files(python / "mobile-destination") == _files(csharp / "mobile-destination")
    assert _files(python / "names") == _files(csharp / "names")
