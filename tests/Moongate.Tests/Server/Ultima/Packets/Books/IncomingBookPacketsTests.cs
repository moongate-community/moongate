using Moongate.Server.Ultima.Packets.Books;

namespace Moongate.Tests.Server.Ultima.Packets.Books;

/// <summary>
///     What the client sends about a book: recognised, so the stream goes on, and never acted upon.
/// </summary>
public sealed class IncomingBookPacketsTests
{
    [Fact]
    public void APageRequest_IsRecognised()
    {
        // ClassicUO asking for page 1: serial, one page, number 1, line count 0xFFFF.
        var data = Convert.FromHexString("66" + "000D" + "40000010" + "0001" + "0001" + "FFFF");

        Assert.True(BookPagesRequestPacket.TryParse(data, out var packet));
        Assert.Equal(0x40000010u, packet.Book.Value);
    }

    [Fact]
    public void AHeaderChange_IsRecognised()
    {
        var data = Convert.FromHexString("D4" + "0011" + "40000010" + "01" + "01" + "0001" + "0001" + "00" + "0001" + "00");

        Assert.True(BookHeaderChangePacket.TryParse(data, out var packet));
        Assert.Equal(0x40000010u, packet.Book.Value);
    }

    [Fact]
    public void AnOldHeaderChange_IsRecognised()
    {
        var data = new byte[99];
        data[0] = 0x93;
        data[1] = 0x40;
        data[4] = 0x10;

        Assert.True(OldBookHeaderChangePacket.TryParse(data, out var packet));
        Assert.Equal(0x40000010u, packet.Book.Value);
    }

    [Theory]
    [InlineData("66000540")]
    [InlineData("D4000540")]
    public void ATruncatedPacket_IsRefused(string hex)
    {
        var data = Convert.FromHexString(hex);

        Assert.False(data[0] == 0x66 ? BookPagesRequestPacket.TryParse(data, out _) : BookHeaderChangePacket.TryParse(data, out _));
    }
}
