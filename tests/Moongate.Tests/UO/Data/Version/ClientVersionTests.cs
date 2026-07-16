using Moongate.UO.Data.Types;
using Moongate.UO.Data.Version;

namespace Moongate.Tests.UO.Data.Version;

public class ClientVersionTests
{
    [Fact]
    public void Comparison_OrdersByVersionComponents()
    {
        Assert.True(ClientVersion.Version70610 > ClientVersion.Version70500);
        Assert.True(ClientVersion.Version70654 >= ClientVersion.Version70610);
        Assert.True(ClientVersion.Version7000 < ClientVersion.Version70610);
    }

    [Fact]
    public void Equality_SameVersionIsEqual()
    {
        var a = new ClientVersion("7.0.0.0");
        var b = new ClientVersion("7.0.0.0");

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void FromPacked_HighMajor_IsClassifiedAsSa()
    {
        // Major byte 0x43 == 67 -> Stygian Abyss client, major folded down by 60.
        var version = ClientVersion.FromPacked(0x43000000);

        Assert.Equal(ClientType.SA, version.Type);
        Assert.Equal(7, version.Major);
    }

    [Fact]
    public void FromPacked_UnpacksTheFourBytes()
    {
        var version = ClientVersion.FromPacked(0x07004104);

        Assert.Equal(7, version.Major);
        Assert.Equal(0, version.Minor);
        Assert.Equal(65, version.Revision);
        Assert.Equal(4, version.Patch);
        Assert.Equal("7.0.65.4", version.ToString());
    }

    [Fact]
    public void Parse_ClassicVersion_ReadsAllComponents()
    {
        var version = new ClientVersion("7.0.65.4");

        Assert.Equal(7, version.Major);
        Assert.Equal(0, version.Minor);
        Assert.Equal(65, version.Revision);
        Assert.Equal(4, version.Patch);

        // The string constructor only tags KR/SA/UOTD; a plain classic version keeps the default type.
        Assert.Equal(ClientType.None, version.Type);
        Assert.Equal("7.0.65.4", version.ToString());
    }

    [Fact]
    public void Parse_Garbage_FallsBackToZeroedClassic()
    {
        var version = new ClientVersion("not-a-version");

        Assert.Equal(0, version.Major);
        Assert.Equal(0, version.Minor);
        Assert.Equal(0, version.Revision);
        Assert.Equal(0, version.Patch);
        Assert.Equal(ClientType.Classic, version.Type);
    }

    [Fact]
    public void Parse_KrVersion_IsClassifiedAsKr()
    {
        var version = new ClientVersion("66.55.53");

        Assert.Equal(ClientType.KR, version.Type);
    }

    [Fact]
    public void Parse_UotdVersion_IsClassifiedAsUotd()
    {
        var version = new ClientVersion("4.0.0a uotd");

        Assert.Equal(ClientType.UOTD, version.Type);
    }

    [Fact]
    public void ProtocolChanges_MapsToTheMatchingBracket()
    {
        Assert.Equal(ProtocolChangesType.Version70610, ClientVersion.Version70654.ProtocolChangesType);
        Assert.Equal(ProtocolChangesType.Version7090, ClientVersion.Version7090.ProtocolChangesType);
        Assert.Equal(ProtocolChangesType.Version7000, ClientVersion.Version7000.ProtocolChangesType);
    }
}
