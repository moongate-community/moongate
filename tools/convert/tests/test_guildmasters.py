"""The guildmasters converter, with the cases of the C# tests it replaces."""

from __future__ import annotations

import io
import tomllib

import pytest

from moongate_convert import guildmasters
from moongate_convert.cli import main

BLACKSMITH = """
public partial class BlacksmithGuildmaster : BaseGuildmaster
{
    public BlacksmithGuildmaster() : base("blacksmith")
    {
        SetSkill(SkillName.ArmsLore, 65.0, 88.0);
        SetSkill(SkillName.Blacksmith, 90.0, 100.0);
        SetSkill(SkillName.Macing, 36.0, 68.0);
        SetSkill(SkillName.Parry, 36.0, 68.0);
    }

    public override NpcGuild NpcGuild => NpcGuild.BlacksmithsGuild;
}
"""


class Workspace:
    def __init__(self, root):
        self.root = root
        self.source = root / "Guildmasters"
        self.items = root / "items"
        self.mobiles = root / "mobiles"
        self.lists = root / "npc_lists"
        self.output = ""
        self.error = ""

    def write(self, name, text):
        self.source.mkdir(parents=True, exist_ok=True)
        (self.source / name).write_text(text, encoding="utf-8")

    def run(self):
        self.items.mkdir(parents=True, exist_ok=True)
        output, error = io.StringIO(), io.StringIO()
        code = main(
            [
                "modernuo-guildmasters",
                "--source", str(self.source),
                "--items", str(self.items),
                "--mobiles", str(self.mobiles),
                "--npc-lists", str(self.lists),
            ],
            output,
            error,
        )
        self.output, self.error = output.getvalue(), error.getvalue()

        return code

    def masters(self):
        return tomllib.loads((self.mobiles / "guildmasters.toml").read_text(encoding="utf-8"))["mobile"]


@pytest.fixture
def workspace(tmp_path):
    return Workspace(tmp_path)


def test_a_guildmaster_becomes_a_man_and_a_woman_with_the_skills_the_title_and_the_guild(workspace):
    workspace.write("BlacksmithGuildmaster.cs", BLACKSMITH)
    workspace.write("BaseGuildmaster.cs", "public abstract class BaseGuildmaster { }")

    assert workspace.run() == 0, workspace.error + workspace.output

    masters = workspace.masters()
    assert [master["id"] for master in masters] == ["m_blacksmith_guildmaster", "f_blacksmith_guildmaster"]
    man = masters[0]
    assert (man["base_id"], man["title"], man["npc_guild"]) == ("basevendor", "the blacksmith guildmaster", "blacksmiths")
    assert masters[1]["title"] == "the blacksmith guildmistress"
    assert sorted(man["skills"]) == ["arms_lore", "blacksmithy", "mace_fighting", "parrying"]
    assert man["skills"]["blacksmithy"] == "1d11+89"

    [entry] = tomllib.loads((workspace.lists / "npclists_guildmasters.toml").read_text(encoding="utf-8"))["npc_list"]
    assert entry["id"] == "blacksmithguildmaster"
    assert [item["mobile_id"] for item in entry["entries"]] == ["m_blacksmith_guildmaster", "f_blacksmith_guildmaster"]


def test_the_outfit_of_a_guildmaster_becomes_its_trade_equipment_in_place_of_the_apron(workspace):
    workspace.items.mkdir(parents=True)
    (workspace.items / "gear.toml").write_text(
        '[[item]]\nid = "0x153d_full_apron"\n[[item]]\nid = "0x13ec_a"\n[[item]]\nid = "0x13ec_lbr"\n'
        '[[item]]\nid = "0x13ec_aos"\n[[item]]\nid = "0x140c_lbr"\n[[item]]\nid = "0x13e3_lbr"\n'
        '[[item]]\nid = "0x1f03_robe"\n[[item]]\nid = "0x13f8_lbr"\n',
        encoding="utf-8",
    )
    workspace.write(
        "BlacksmithGuildmaster.cs",
        """
public partial class BlacksmithGuildmaster : BaseGuildmaster
{
    public BlacksmithGuildmaster() : base("blacksmith")
    {
        SetSkill(SkillName.Blacksmith, 90.0, 100.0);
    }

    public override NpcGuild NpcGuild => NpcGuild.BlacksmithsGuild;

    public override void InitOutfit()
    {
        base.InitOutfit();

        Item item = Utility.RandomBool() ? new FullApron() : new RingmailChest();

        if (!EquipItem(item))
        {
            item.Delete();
        }

        AddItem(new Bascinet());
        AddItem(new SmithHammer());
    }
}
""",
    )
    workspace.write(
        "MageGuildmaster.cs",
        """
public partial class MageGuildmaster : BaseGuildmaster
{
    public MageGuildmaster() : base("mage")
    {
        SetSkill(SkillName.Magery, 90.0, 100.0);
    }

    public override NpcGuild NpcGuild => NpcGuild.MagesGuild;

    public override void InitOutfit()
    {
        base.InitOutfit();

        AddItem(new Robe(Utility.RandomBlueHue()));
        AddItem(new GnarledStaff());
    }
}
""",
    )
    workspace.write(
        "ThiefGuildmaster.cs",
        """
public partial class ThiefGuildmaster : BaseGuildmaster
{
    public ThiefGuildmaster() : base("thief")
    {
        SetSkill(SkillName.Stealing, 90.0, 100.0);
    }

    public override NpcGuild NpcGuild => NpcGuild.ThievesGuild;

    public override void InitOutfit()
    {
        base.InitOutfit();

        if (Utility.RandomBool())
        {
            AddItem(new Kryss());
        }
        else
        {
            AddItem(new Dagger());
        }
    }
}
""",
    )

    assert workspace.run() == 0, workspace.error + workspace.output

    masters = {master["id"]: master for master in workspace.masters()}
    smith = masters["m_blacksmith_guildmaster"]["equipment"]
    # The plain piece of the first era is worn, and the apron or chest piece replaces the generic apron entry.
    assert [entry["items"] for entry in smith[3:]] == [["0x153d_full_apron", "0x13ec_lbr"], ["0x140c_lbr"], ["0x13e3_lbr"]]
    assert not any("0x1fa1_tunic" in entry["items"] for entry in smith)
    mage = masters["f_mage_guildmaster"]["equipment"]
    assert mage[3]["items"] == ["0x1f03_robe"]
    assert mage[4]["items"] == ["0x13f8_lbr"]
    assert mage[3]["hue"] == "0x0515-0x054A"
    # No template for a kryss or a dagger: the thief wears the apron, and the report says so.
    assert any("0x1fa1_tunic" in entry["items"] for entry in masters["m_thief_guildmaster"]["equipment"])
    assert "no item template for the outfit item Kryss" in workspace.output


def test_a_guildmaster_that_names_no_guild_teaches_only_and_the_report_says_so(workspace):
    workspace.write(
        "MinerGuildmaster.cs",
        """
public partial class MinerGuildmaster : BaseGuildmaster
{
    public MinerGuildmaster() : base("miner")
    {
        SetSkill(SkillName.Mining, 90.0, 100.0);
    }
}
""",
    )

    assert workspace.run() == 0, workspace.error + workspace.output

    assert all("npc_guild" not in master for master in workspace.masters())
    assert "names no guild" in workspace.output


def test_a_folder_with_no_guildmaster_exits_with_an_error(workspace):
    workspace.source.mkdir()

    assert workspace.run() == 2
    assert "no guildmaster" in workspace.error


def test_a_class_with_no_title_is_counted_and_left_out(workspace):
    workspace.write("OddGuildmaster.cs", "public class OddGuildmaster : BaseGuildmaster { public OddGuildmaster() : base(GetTitle()) { } }")
    workspace.write("BlacksmithGuildmaster.cs", BLACKSMITH)

    assert workspace.run() == 0, workspace.error

    assert "OddGuildmaster.cs has no constructor with a title" in workspace.output
    assert [master["id"] for master in workspace.masters()] == ["m_blacksmith_guildmaster", "f_blacksmith_guildmaster"]


def test_a_skill_of_modernuo_that_the_server_lacks_is_counted(workspace):
    workspace.write(
        "OddGuildmaster.cs",
        'public class OddGuildmaster : BaseGuildmaster { public OddGuildmaster() : base("odd") { SetSkill(SkillName.Cooking, 60.0, 80.0); SetSkill(SkillName.Mastery, 60.0, 80.0); } }',
    )

    assert workspace.run() == 0, workspace.error

    assert "skill Mastery is not one of this server's" in workspace.output
    assert list(workspace.masters()[0]["skills"]) == ["cooking"]


@pytest.mark.parametrize(
    ("low", "high", "expected"),
    [(90, 100, "1d11+89"), (80, 100, "1d21+79"), (36, 68, "1d33+35"), (60, 60, 60), (100, 90, "1d11+89"), (0, 5, "1d6-1")],
)
def test_a_range_of_skill_is_written_as_dice(low, high, expected):
    assert guildmasters.to_dice(low, high) == expected
