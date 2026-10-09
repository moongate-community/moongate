"""The mobile pass of the ``uox`` command: UOX3's NPCs, creatures and name lists as mobile templates and ``names.toml``.

The tests of ``UoxMobileConverterTests`` of the C# converter, ported.
"""

from __future__ import annotations

import io
import tomllib
from pathlib import Path

import pytest

from moongate_convert import uox
from moongate_convert.specs import DiceSpec

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
