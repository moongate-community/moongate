using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Types.Clients;

namespace Moongate.Network.Packets.Tests.Data.Clients;

public class ClientVersionTests
{
    [Fact]
    public void Compare_EnhancedClient_IgnoresThePatch()
    {
        var enhanced = ClientVersion.Parse("67.0.13.0");
        var classic = ClientVersion.Parse("7.0.13.5");

        Assert.Equal(0, enhanced.CompareTo(classic));
        Assert.True(enhanced >= ClientVersion.Version70130);
    }

    [Fact]
    public void Compare_OrdersByMajorMinorRevisionThenPatch()
    {
        Assert.True(ClientVersion.Parse("7.0.117.0") > ClientVersion.Version70130);
        Assert.True(ClientVersion.Parse("7.0.12.9") < ClientVersion.Version70130);
        Assert.True(ClientVersion.Parse("7.0.13.1") > ClientVersion.Version70130);
        Assert.True(ClientVersion.Parse("6.9.99.99") < ClientVersion.Parse("7.0.0.0"));
        Assert.True(ClientVersion.Version70130 <= ClientVersion.Parse("7.0.13"));
        Assert.True(null < ClientVersion.Version70130);
    }

    [Fact]
    public void Constructor_NegativePart_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClientVersion(7, -1, 0, 0));
    }

    [Theory,
     InlineData(7, ClientType.Classic, 7),
     InlineData(66, ClientType.Kr, 66),
     InlineData(67, ClientType.Enhanced, 7)]
    public void Constructor_ReportedMajor_SetsTypeAndMajor(int reportedMajor, ClientType type, int major)
    {
        var version = new ClientVersion(reportedMajor, 0, 105, 0);

        Assert.Equal(type, version.Type);
        Assert.Equal(major, version.Major);
    }

    [Fact]
    public void Equality_ComparesEveryPartAndTheType()
    {
        Assert.Equal(ClientVersion.Parse("7.0.117"), ClientVersion.Parse("7.0.117.0"));
        Assert.True(ClientVersion.Parse("7.0.117") == new ClientVersion(7, 0, 117, 0));
        Assert.NotEqual(ClientVersion.Parse("7.0.117.0"), ClientVersion.Parse("7.0.117.1"));
        Assert.NotEqual(ClientVersion.Parse("7.0.13.0"), ClientVersion.Parse("67.0.13.0"));
        Assert.Equal(
            ClientVersion.Parse("7.0.117").GetHashCode(),
            ClientVersion.Parse("7.0.117.0").GetHashCode()
        );
    }

    [Theory,
     InlineData("7.0.117.0", 7, 0, 117, 0),
     InlineData("7.0.117", 7, 0, 117, 0),
     InlineData("7.0", 7, 0, 0, 0),
     InlineData(" 7.0.98.1 ", 7, 0, 98, 1),
     InlineData("4.0.7a", 4, 0, 7, 1),
     InlineData("4.0.7c", 4, 0, 7, 3)]
    public void Parse_ValidText_ReadsEveryPart(string text, int major, int minor, int revision, int patch)
    {
        var version = ClientVersion.Parse(text);

        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(revision, version.Revision);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(ClientType.Classic, version.Type);
    }

    [Fact]
    public void Parse_UnreadableText_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ClientVersion.Parse("seven"));
    }

    [Theory,
     InlineData("7.0.117.0", "7.0.117.0"),
     InlineData("7.0.117", "7.0.117.0"),
     InlineData("4.0.7a", "4.0.7.1"),
     InlineData("67.0.105.0", "67.0.105.0")]
    public void ToString_WritesFourNumbersThatParseBackUnchanged(string text, string expected)
    {
        var version = ClientVersion.Parse(text);

        Assert.Equal(expected, version.ToString());
        Assert.Equal(version, ClientVersion.Parse(version.ToString()));
    }

    [Theory,
     InlineData(null),
     InlineData(""),
     InlineData("   "),
     InlineData("7"),
     InlineData("7.0.1.2.3"),
     InlineData("7..1"),
     InlineData("7.0.-1"),
     InlineData("7.0.1a.2"),
     InlineData("7.0.1.2a"),
     InlineData("a.0.1"),
     InlineData("7.0.1 uotd")]
    public void TryParse_UnreadableText_ReturnsFalse(string? text)
    {
        Assert.False(ClientVersion.TryParse(text, out var version));
        Assert.Null(version);
    }
}
