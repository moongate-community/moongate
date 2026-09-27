using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Data.Templates.Items;

public sealed class ItemTemplateTests
{
    // The TOML form of an ItemTemplate depends on the global converters; register them before writing, so a parallel
    // test registering one between the write and the read cannot change the format under this test.
    public ItemTemplateTests()
    {
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
    }

    [Fact]
    public void Visibility_Default_IsUnsetAndVisibleToEveryone()
    {
        var template = new ItemTemplate();

        Assert.Null(template.Visibility);
        Assert.True(template.IsVisibleTo(AccountType.Regular));
    }

    [Theory,
     InlineData(AccountType.Regular, AccountType.Regular, true),
     InlineData(AccountType.GameMaster, AccountType.Regular, false),
     InlineData(AccountType.GameMaster, AccountType.GameMaster, true),
     InlineData(AccountType.GameMaster, AccountType.Administrator, true),
     InlineData(AccountType.Administrator, AccountType.GameMaster, false),
     InlineData(AccountType.Administrator, AccountType.Administrator, true)]
    public void IsVisibleTo_AccountAtOrAboveTheVisibility_SeesTheItem(
        AccountType visibility,
        AccountType viewer,
        bool visible
    )
    {
        Assert.Equal(visible, new ItemTemplate { Visibility = visibility }.IsVisibleTo(viewer));
    }

    [Theory,
     InlineData(AccountType.Regular, "regular"),
     InlineData(AccountType.GameMaster, "game_master"),
     InlineData(AccountType.Administrator, "administrator")]
    public void Visibility_Toml_RoundTripsAsText(AccountType visibility, string text)
    {
        var toml = TomlUtils.Serialize(new ItemTemplate { Id = "spawner", Visibility = visibility });

        Assert.Contains($"visibility = \"{text}\"", toml);
        Assert.Equal(visibility, TomlUtils.Deserialize<ItemTemplate>(toml)!.Visibility);
    }

    [Fact]
    public void Visibility_TomlWithoutTheField_IsUnsetAndVisibleToEveryone()
    {
        var template = TomlUtils.Deserialize<ItemTemplate>("id = \"lamp\"\n")!;

        Assert.Null(template.Visibility);
        Assert.True(template.IsVisibleTo(AccountType.Regular));
    }

    [Fact]
    public void Visibility_Unset_IsNotWrittenSoTheBaseTemplateDecides()
    {
        Assert.DoesNotContain("visibility", TomlUtils.Serialize(new ItemTemplate { Id = "orcspawn", BaseId = "base_spawner" }));
    }

    [Theory, InlineData("gm"), InlineData("7")]
    public void Visibility_TomlUnknownValue_Throws(string text)
    {
        var toml = text == "7" ? "id = \"s\"\nvisibility = 7\n" : $"id = \"s\"\nvisibility = \"{text}\"\n";

        Assert.ThrowsAny<Exception>(() => TomlUtils.Deserialize<ItemTemplate>(toml));
    }

    [Fact]
    public void BaseFields_Toml_RoundTrip()
    {
        const string toml = """
                            id = "gold"
                            item_id = 3821
                            weight = 0.02
                            amount = "10-20"
                            stackable = true
                            layer = "one_handed"
                            buy_price = 60
                            sell_price = 30
                            decays = false
                            decay_minutes = 90
                            loot_type = "blessed"

                            [tags]
                            quest_step = "3"
                            """;

        var template = TomlUtils.Deserialize<ItemTemplate>(toml)!;
        var back = TomlUtils.Deserialize<ItemTemplate>(TomlUtils.Serialize(template))!;

        foreach (var loaded in new[] { template, back })
        {
            Assert.Equal(0.02m, loaded.Weight);
            Assert.True(loaded.Amount!.Value.IsRandom);
            Assert.InRange(loaded.Amount.Value.Resolve(), 10, 20);
            Assert.True(loaded.Stackable);
            Assert.Equal(LayerType.OneHanded, loaded.Layer);
            Assert.Equal((60, 30), (loaded.BuyPrice, loaded.SellPrice));
            Assert.False(loaded.Decays);
            Assert.Equal(90, loaded.DecayMinutes);
            Assert.Equal(LootType.Blessed, loaded.LootType);
            Assert.Equal("3", loaded.Tags!["quest_step"]);
        }
    }

    [Fact]
    public void Weight_AWholeNumber_Reads()
    {
        Assert.Equal(7m, TomlUtils.Deserialize<ItemTemplate>("id = \"anvil\"\nweight = 7\n")!.Weight);
    }

    [Fact]
    public void BaseFields_Unset_AreNotWritten()
    {
        var toml = TomlUtils.Serialize(new ItemTemplate { Id = "lamp" });

        foreach (var key in new[] { "weight", "amount", "stackable", "layer", "buy_price", "sell_price", "decays", "decay_minutes", "loot_type", "tags", "movable" })
        {
            Assert.DoesNotContain(key + " ", toml);
        }
    }

    [Theory,
     InlineData("weight", "-1"),
     InlineData("weight", "0.005"),
     InlineData("amount", "0"),
     InlineData("buy_price", "-1"),
     InlineData("sell_price", "-5"),
     InlineData("decay_minutes", "0")]
    public void Validate_ABadValue_NamesTheTemplateAndField(string field, string value)
    {
        var template = TomlUtils.Deserialize<ItemTemplate>($"id = \"bad\"\nitem_id = 1\n{field} = {value}\n")!;

        var exception = Assert.Throws<InvalidDataException>(template.Validate);

        Assert.Contains("bad", exception.Message);
        Assert.Contains(field, exception.Message);
    }

    [Fact]
    public void Validate_AnEmptyTagKey_Throws()
    {
        var template = new ItemTemplate { Id = "bad", Tags = new() { [""] = "x" } };

        Assert.Throws<InvalidDataException>(template.Validate);
    }

    [Fact]
    public void Validate_AValidTemplate_Passes()
    {
        new ItemTemplate { Id = "ok", Weight = 7, BuyPrice = 1, DecayMinutes = 1 }.Validate();
    }
}
