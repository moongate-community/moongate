"""The vendors converter, with the cases of the C# tests it replaces."""

from __future__ import annotations

import io
import tomllib

import pytest

from moongate_convert.cli import main

BAKER_SHOP = """
using System.Collections.Generic;
using Server.Items;

namespace Server.Mobiles
{
    public class SBBaker : SBInfo
    {
        public override IShopSellInfo SellInfo { get; } = new InternalSellInfo();
        public override List<GenericBuyInfo> BuyInfo { get; } = new InternalBuyInfo();

        public class InternalBuyInfo : List<GenericBuyInfo>
        {
            public InternalBuyInfo()
            {
                Add(new GenericBuyInfo(typeof(BreadLoaf), 6, 20, 0x103B, 0));
                Add(new GenericBuyInfo(typeof(BreadLoaf), 5, 20, 0x103C, 0)); // a comment
                Add(new GenericBuyInfo("a fine cake", typeof(Cake), 13, 10, 0x9E9, 0x44));
                Add(new GenericBuyInfo(typeof(Muffins), 3, 20, 0x9EA, Utility.RandomBlueHue()));
                Add(new BeverageBuyInfo(typeof(Jug), BeverageType.Cider, 13, 20, 0x9C8, 0));
                Add(new AnimalBuyInfo(1, "a cat", typeof(Cat), 132, 10, 201, 0));
                Add(new GenericBuyInfo(typeof(Ghost), 9, 5, 0x7777, 0));
                if (Core.AOS)
                {
                    Add(new GenericBuyInfo(typeof(Pie), 7, 5, 0x1041, 0));
                }
            }
        }

        public class InternalSellInfo : GenericSellInfo
        {
            public InternalSellInfo()
            {
                Add(typeof(BreadLoaf), 3);
                Add(typeof(Cake), 5);
                Add(typeof(Pie), 4);
                Add(typeof(Mystery), 9);
            }
        }
    }
}
"""

BAKER_VENDOR = """
namespace Server.Mobiles
{
    public class Baker : BaseVendor
    {
        private readonly List<SBInfo> m_SBInfos = new();

        protected override List<SBInfo> SBInfos => m_SBInfos;

        public override void InitSBInfo()
        {
            m_SBInfos.Add(new SBBaker());
            if (IsTokunoVendor)
            {
                m_SBInfos.Add(new SBSEFood());
            }
        }
    }
}
"""


class Workspace:
    def __init__(self, root):
        self.root = root
        self.source = root / "Vendors"
        self.destination = root / "shops"
        self.output = ""
        self.error = ""

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def prepare(self, vendor=None, mobiles=None):
        self.write("Vendors/SBInfo/SBBaker.cs", BAKER_SHOP)
        self.write("Vendors/NPC/Baker.cs", vendor or BAKER_VENDOR)
        self.write(
            "items/food.toml",
            '[[item]]\nid = "0x103b_bread_loaf"\n[[item]]\nid = "0x103c_bread_loaf"\n[[item]]\nid = "0x09e9_cake"\n'
            '[[item]]\nid = "0x1041_pie"\n[[item]]\nid = "0x09ea_muffin"\n[[item]]\nid = "0x09c8_jug"\n[[item]]\nid = "0x1041_baked_pie"\n',
        )
        self.write("mobiles/vendors.toml", mobiles or '[[mobile]]\nid = "baker"\n[[mobile]]\nid = "m_baker"\n[[mobile]]\nid = "f_baker"\n')

    def run(self):
        output, error = io.StringIO(), io.StringIO()
        code = main(
            [
                "modernuo-vendors",
                "--source", str(self.source),
                "--items", str(self.root / "items"),
                "--mobiles", str(self.root / "mobiles"),
                "--destination", str(self.destination),
            ],
            output,
            error,
        )
        self.output, self.error = output.getvalue(), error.getvalue()

        return code

    def shop(self, name):
        return tomllib.loads((self.destination / f"{name}.toml").read_text(encoding="utf-8"))["shop"]


@pytest.fixture
def workspace(tmp_path):
    return Workspace(tmp_path)


def test_a_vendor_with_its_shop_becomes_a_file_of_lines_by_graphic(workspace):
    workspace.prepare()

    assert workspace.run() == 0, workspace.error

    [shop] = workspace.shop("baker")
    assert shop["id"] == "baker"
    assert shop["vendors"] == ["baker", "m_baker", "f_baker"]
    assert [(line["item"], line["price"], line["amount"], line["hue"], line["name"]) for line in shop["buy"]] == [
        ("0x103b_bread_loaf", 6, 20, 0, ""),
        ("0x103c_bread_loaf", 5, 20, 0, ""),
        ("0x09e9_cake", 13, 10, 0x44, "a fine cake"),
        ("0x09ea_muffin", 3, 20, 0, ""),
        ("0x09c8_jug", 13, 20, 0, ""),
    ]


def test_the_sell_table_becomes_sell_lines_by_the_graphics_the_type_is_sold_under_else_by_name(workspace):
    workspace.prepare()

    assert workspace.run() == 0, workspace.error

    # BreadLoaf is sold under two graphics; Pie is no type of any buy line but a template is named like it.
    assert [(line["item"], line["price"]) for line in workspace.shop("baker")[0]["sell"]] == [
        ("0x103b_bread_loaf", 3),
        ("0x103c_bread_loaf", 3),
        ("0x09e9_cake", 5),
        ("0x1041_pie", 4),
    ]
    assert "no item template for the sold type Mystery" in workspace.output


def test_an_armor_graphic_is_bought_as_the_plain_piece_of_its_first_era_and_sold_in_every_material(workspace):
    workspace.write(
        "Vendors/SBInfo/SBBaker.cs",
        """
public class SBBaker : SBInfo
{
    public class InternalBuyInfo : List<GenericBuyInfo>
    {
        public InternalBuyInfo()
        {
            Add(new GenericBuyInfo(typeof(ChainChest), 140, 5, 0x13BF, 0));
        }
    }

    public class InternalSellInfo : GenericSellInfo
    {
        public InternalSellInfo()
        {
            Add(typeof(ChainChest), 70);
        }
    }
}
""",
    )
    workspace.write("Vendors/NPC/Baker.cs", BAKER_VENDOR)
    workspace.write("items/armor.toml", '[[item]]\nid = "0x13bf_aos"\n[[item]]\nid = "0x13bf_a"\n[[item]]\nid = "0x13bf_lbr"\n[[item]]\nid = "0x13bf_tol"\n')
    workspace.write("mobiles/vendors.toml", '[[mobile]]\nid = "baker"\n')

    assert workspace.run() == 0, workspace.error

    [shop] = workspace.shop("baker")
    assert [line["item"] for line in shop["buy"]] == ["0x13bf_lbr"]
    assert sorted(line["item"] for line in shop["sell"]) == ["0x13bf_a", "0x13bf_aos", "0x13bf_lbr", "0x13bf_tol"]
    assert all(line["price"] == 70 for line in shop["sell"])


def test_a_graphic_of_materials_only_is_not_bought_but_sold_in_every_material(workspace):
    workspace.write(
        "Vendors/SBInfo/SBBaker.cs",
        """
public class SBBaker : SBInfo
{
    public class InternalBuyInfo : List<GenericBuyInfo>
    {
        public InternalBuyInfo()
        {
            Add(new GenericBuyInfo(typeof(Bracers), 80, 5, 0x1409, 0));
            Add(new GenericBuyInfo(typeof(BreadLoaf), 6, 20, 0x103B, 0));
        }
    }

    public class InternalSellInfo : GenericSellInfo
    {
        public InternalSellInfo()
        {
            Add(typeof(Bracers), 40);
        }
    }
}
""",
    )
    workspace.write("Vendors/NPC/Baker.cs", BAKER_VENDOR)
    workspace.write("items/armor.toml", '[[item]]\nid = "0x1409_a"\n[[item]]\nid = "0x1409_g"\n[[item]]\nid = "0x103b_bread_loaf"\n')
    workspace.write("mobiles/vendors.toml", '[[mobile]]\nid = "baker"\n')

    assert workspace.run() == 0, workspace.error

    [shop] = workspace.shop("baker")
    assert [line["item"] for line in shop["buy"]] == ["0x103b_bread_loaf"]
    assert sorted(line["item"] for line in shop["sell"]) == ["0x1409_a", "0x1409_g"]
    assert "only material templates" in workspace.output


def test_a_price_a_sell_line_pays_above_the_lowest_price_it_is_sold_at_is_lowered_so_no_one_profits(workspace):
    workspace.prepare()
    # The baker pays 9 for bread; the same loaf is sold at 6 by this baker.
    workspace.write("Vendors/SBInfo/SBBaker.cs", BAKER_SHOP.replace("Add(typeof(BreadLoaf), 3);", "Add(typeof(BreadLoaf), 9);"))

    assert workspace.run() == 0, workspace.error

    sell = workspace.shop("baker")[0]["sell"]
    assert next(line for line in sell if line["item"] == "0x103b_bread_loaf")["price"] == 6
    assert next(line for line in sell if line["item"] == "0x103c_bread_loaf")["price"] == 5
    assert "lowered to" in workspace.output


def test_what_it_cannot_map_is_dropped_and_the_report_says_why(workspace):
    workspace.prepare()

    assert workspace.run() == 0, workspace.error

    for reason in (
        "no item template for graphic 0x7777 (Ghost)",
        "animal line",
        "conditional line",
        "conditional SBInfo SBSEFood",
        "hue that is no literal",
        "beverage content dropped",
    ):
        assert reason in workspace.output
    assert not any("pie" in line["item"] for line in workspace.shop("baker")[0]["buy"])


def test_the_file_tells_its_purpose_and_fields_and_has_no_source_line(workspace):
    workspace.prepare()

    assert workspace.run() == 0, workspace.error

    text = (workspace.destination / "baker.toml").read_text(encoding="utf-8")
    assert text.startswith("# What it is for:")
    assert "# Fields:" in text
    assert "Source:" not in text


def test_a_vendor_with_no_mobile_template_gets_no_shop(workspace):
    workspace.prepare(mobiles='id = "orc"\n')

    assert workspace.run() == 2

    assert "no vendor with a shop" in workspace.error
    assert not (workspace.destination / "baker.toml").exists()


def test_the_shops_of_a_vendor_are_the_union_of_its_switch_sets(workspace):
    workspace.prepare(
        vendor="""
public class Baker : BaseVendor
{
    public override void InitSBInfo()
    {
        switch (Utility.Random(2))
        {
            case 0: { m_SBInfos.Add(new SBBaker()); break; }
            case 1: { m_SBInfos.Add(new SBBaker()); m_SBInfos.Add(new SBBaker()); break; }
        }
    }
}
"""
    )

    assert workspace.run() == 0, workspace.error

    # The same line from two SBInfo of a vendor is listed once.
    assert len(workspace.shop("baker")[0]["buy"]) == 5


def test_a_source_that_is_no_folder_of_modernuo_exits_with_an_error(workspace):
    workspace.source.mkdir(parents=True)

    assert workspace.run() == 2
    assert "SBInfo" in workspace.error


def test_a_sell_line_with_no_sell_lines_writes_an_empty_list():
    from moongate_convert.vendors import Shop, ShopLine, serialize

    text = serialize(Shop("baker", ["baker"], [ShopLine("0x103b_bread_loaf", 6, 20)], []))

    assert tomllib.loads(text)["shop"][0]["sell"] == []
    assert text.index("sell = []") < text.index("[[shop.buy]]")


def test_a_syntax_error_in_a_source_exits_with_an_error(workspace):
    workspace.prepare()
    workspace.write("Vendors/NPC/Broken.cs", "public class Broken { void M( }")

    assert workspace.run() == 2
    assert "Broken.cs" in workspace.error
