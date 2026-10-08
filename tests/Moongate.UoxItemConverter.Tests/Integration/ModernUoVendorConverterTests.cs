using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.UoxItemConverter.Internal;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoVendorConverterTests : IDisposable
{
    private const string BakerShop =
        """
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
        """;

    private const string BakerVendor =
        """
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
        """;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "moongate-modernuo-vendors-" + Guid.NewGuid().ToString("N")
    );

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Source => Path.Combine(_root, "Vendors");

    private string Destination => Path.Combine(_root, "shops");

    private string CombinedOutput => _output + _error.ToString();

    [Fact]
    public void Run_AVendorWithItsShop_BecomesAFileOfLines_ByGraphic()
    {
        Prepare();

        Assert.True(Run() == 0, CombinedOutput);

        var shop = Assert.Single(Read("baker").Shop);
        Assert.Equal("baker", shop.Id);
        Assert.Equal(["baker", "m_baker", "f_baker"], shop.Vendors);
        Assert.Equal(
            [
                ("0x103b_bread_loaf", 6, 20, 0, ""), ("0x103c_bread_loaf", 5, 20, 0, ""),
                ("0x09e9_cake", 13, 10, 0x44, "a fine cake"), ("0x09ea_muffin", 3, 20, 0, ""),
                ("0x09c8_jug", 13, 20, 0, "")
            ],
            shop.Buy.Select(line => (line.Item, line.Price, line.Amount, line.Hue, line.Name))
        );
    }

    [Fact]
    public void Run_TheSellTable_BecomesSellLines_ByTheGraphicsTheTypeIsSoldUnder_ElseByName()
    {
        Prepare();

        Assert.True(Run() == 0, CombinedOutput);

        // BreadLoaf is sold under two graphics; Pie is no type of any buy line but a template is named like it.
        Assert.Equal(
            [("0x103b_bread_loaf", 3), ("0x103c_bread_loaf", 3), ("0x09e9_cake", 5), ("0x1041_pie", 4)],
            Read("baker").Shop[0].Sell.Select(line => (line.Item, line.Price))
        );
        Assert.Contains("no item template for the sold type Mystery", _output.ToString());
    }

    [Fact]
    public void Run_AnArmorGraphic_IsBoughtAsThePlainPieceOfItsFirstEra_AndSoldInEveryMaterial()
    {
        Write(
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
            """
        );
        Write("Vendors/NPC/Baker.cs", BakerVendor);
        Write(
            "items/armor.toml",
            "[[item]]\nid = \"0x13bf_aos\"\n[[item]]\nid = \"0x13bf_a\"\n[[item]]\nid = \"0x13bf_lbr\"\n[[item]]\nid = \"0x13bf_tol\"\n"
        );
        Write("mobiles/vendors.toml", "[[mobile]]\nid = \"baker\"\n");

        Assert.True(Run() == 0, CombinedOutput);

        var shop = Read("baker").Shop[0];
        Assert.Equal("0x13bf_lbr", Assert.Single(shop.Buy).Item);
        Assert.Equal(
            ["0x13bf_a", "0x13bf_aos", "0x13bf_lbr", "0x13bf_tol"],
            shop.Sell.Select(line => line.Item).Order(StringComparer.Ordinal)
        );
        Assert.All(shop.Sell, line => Assert.Equal(70, line.Price));
    }

    [Fact]
    public void Run_AGraphicOfMaterialsOnly_IsNotBought_ButSoldInEveryMaterial()
    {
        Write(
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
            """
        );
        Write("Vendors/NPC/Baker.cs", BakerVendor);
        Write(
            "items/armor.toml",
            "[[item]]\nid = \"0x1409_a\"\n[[item]]\nid = \"0x1409_g\"\n[[item]]\nid = \"0x103b_bread_loaf\"\n"
        );
        Write("mobiles/vendors.toml", "[[mobile]]\nid = \"baker\"\n");

        Assert.True(Run() == 0, CombinedOutput);

        var shop = Read("baker").Shop[0];
        Assert.Equal("0x103b_bread_loaf", Assert.Single(shop.Buy).Item);
        Assert.Equal(["0x1409_a", "0x1409_g"], shop.Sell.Select(line => line.Item).Order(StringComparer.Ordinal));
        Assert.Contains("only material templates", _output.ToString());
    }

    [Fact]
    public void Run_WhatItCannotMap_IsDropped_AndTheReportSaysWhy()
    {
        Prepare();

        Assert.True(Run() == 0, CombinedOutput);

        var report = _output.ToString();
        Assert.Contains("no item template for graphic 0x7777 (Ghost)", report);
        Assert.Contains("animal line", report);
        Assert.Contains("conditional line", report);
        Assert.Contains("conditional SBInfo SBSEFood", report);
        Assert.Contains("hue that is no literal", report);
        Assert.Contains("beverage content dropped", report);
        Assert.DoesNotContain(Read("baker").Shop[0].Buy, line => line.Item.Contains("pie"));
    }

    [Fact]
    public void Run_TheFileTellsItsPurposeAndFields_AndHasNoSourceLine()
    {
        Prepare();

        Assert.True(Run() == 0, CombinedOutput);

        var text = File.ReadAllText(Path.Combine(Destination, "baker.toml"));
        Assert.StartsWith("# What it is for:", text);
        Assert.Contains("# Fields:", text);
        Assert.DoesNotContain("Source:", text);
    }

    [Fact]
    public void Run_AVendorWithNoMobileTemplate_GetsNoShop()
    {
        Prepare(mobiles: "id = \"orc\"\n");

        Assert.Equal(2, Run());

        Assert.Contains("no vendor with a shop", _error.ToString());
        Assert.False(File.Exists(Path.Combine(Destination, "baker.toml")));
    }

    [Fact]
    public void Run_TheShopsOfAVendor_AreTheUnionOfItsSwitchSets()
    {
        Prepare(
            vendor:
            """
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
        );

        Assert.True(Run() == 0, CombinedOutput);

        // The same line from two SBInfo of a vendor is listed once.
        Assert.Equal(5, Read("baker").Shop[0].Buy.Count);
    }

    [Fact]
    public void Run_ASourceThatIsNoFolderOfModernUo_ExitsWithAnError()
    {
        Directory.CreateDirectory(Source);

        Assert.Equal(2, Run());
        Assert.Contains("SBInfo", _error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private void Prepare(string? vendor = null, string? mobiles = null)
    {
        Write("Vendors/SBInfo/SBBaker.cs", BakerShop);
        Write("Vendors/NPC/Baker.cs", vendor ?? BakerVendor);
        Write(
            "items/food.toml",
            "[[item]]\nid = \"0x103b_bread_loaf\"\n[[item]]\nid = \"0x103c_bread_loaf\"\n[[item]]\nid = \"0x09e9_cake\"\n" +
            "[[item]]\nid = \"0x1041_pie\"\n[[item]]\nid = \"0x09ea_muffin\"\n[[item]]\nid = \"0x09c8_jug\"\n[[item]]\nid = \"0x1041_baked_pie\"\n"
        );
        Write(
            "mobiles/vendors.toml",
            mobiles ?? "[[mobile]]\nid = \"baker\"\n[[mobile]]\nid = \"m_baker\"\n[[mobile]]\nid = \"f_baker\"\n"
        );
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private int Run()
    {
        return ModernUoVendorConverter.Run(
            Source,
            Path.Combine(_root, "items"),
            Path.Combine(_root, "mobiles"),
            Destination,
            _output,
            _error
        );
    }

    private ShopFile Read(string shop)
    {
        return TomlUtils.DeserializeFromFile<ShopFile>(Path.Combine(Destination, shop + ".toml"))!;
    }
}
