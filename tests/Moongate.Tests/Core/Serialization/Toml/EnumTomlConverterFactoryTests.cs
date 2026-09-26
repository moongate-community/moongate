using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.Support.Serialization.Data;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class EnumTomlConverterFactoryTests
{
    [Fact]
    public void Serialize_Enums_WritesNamesNeverNumbers()
    {
        var toml = TomlUtils.Serialize(
            new EnumHolder
            {
                Account = AccountType.GameMaster,
                Optional = AccountType.Administrator,
                Flags = TileFlagType.Impassable | TileFlagType.Surface
            }
        );

        Assert.Contains("account = \"game_master\"", toml);
        Assert.Contains("optional = \"administrator\"", toml);
        Assert.Contains("flags = \"", toml);
        Assert.Contains("impassable", toml);
        Assert.Contains("surface", toml);
    }

    [Fact]
    public void Serialize_AnUnsetOptionalEnum_LeavesTheKeyOut()
    {
        Assert.DoesNotContain("optional", TomlUtils.Serialize(new EnumHolder()));
    }

    [Fact]
    public void Deserialize_NamesInAnyForm_ReadsEveryKey()
    {
        var holder = TomlUtils.Deserialize<EnumHolder>(
            """
            account = "GameMaster"
            after = "kept"
            optional = "administrator"
            flags = "surface | impassable"
            last = 7
            """
        )!;

        Assert.Equal(AccountType.GameMaster, holder.Account);
        Assert.Equal("kept", holder.After);
        Assert.Equal(AccountType.Administrator, holder.Optional);
        Assert.Equal(TileFlagType.Impassable | TileFlagType.Surface, holder.Flags);
        Assert.Equal(7, holder.Last);
    }

    [Fact]
    public void SerializeThenDeserialize_RoundTrips()
    {
        var original = new EnumHolder
        {
            Account = AccountType.Administrator,
            After = "x",
            Flags = TileFlagType.Wet | TileFlagType.Impassable,
            Last = 3
        };

        var back = TomlUtils.Deserialize<EnumHolder>(TomlUtils.Serialize(original))!;

        Assert.Equal((original.Account, original.After, original.Optional, original.Flags, original.Last),
            (back.Account, back.After, back.Optional, back.Flags, back.Last));
    }

    [Theory, InlineData("account = 7"), InlineData("account = \"1\""), InlineData("account = \"admin\""), InlineData("flags = \"wet|\"")]
    public void Deserialize_ANumberOrAnUnknownName_Throws(string toml)
    {
        Assert.ThrowsAny<Exception>(() => TomlUtils.Deserialize<EnumHolder>(toml));
    }

    [Fact]
    public void Deserialize_ABareIntegerOfAMember_ReadsItAndWritesTheName()
    {
        var holder = TomlUtils.Deserialize<EnumHolder>("account = 1\n")!;

        Assert.Equal(AccountType.GameMaster, holder.Account);
        Assert.Contains("account = \"game_master\"", TomlUtils.Serialize(holder));
    }
}
