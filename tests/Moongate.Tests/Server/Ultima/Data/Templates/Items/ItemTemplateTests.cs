using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Templates.Items;

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

    [Theory, InlineData("gm"), InlineData("1")]
    public void Visibility_TomlUnknownValue_Throws(string text)
    {
        var toml = text == "1" ? "id = \"s\"\nvisibility = 1\n" : $"id = \"s\"\nvisibility = \"{text}\"\n";

        Assert.ThrowsAny<Exception>(() => TomlUtils.Deserialize<ItemTemplate>(toml));
    }
}
