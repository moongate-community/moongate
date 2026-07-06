using Moongate.Network.Protocol;
using Moongate.Network.Types;

namespace Moongate.Tests.Network;

public class PacketsInfoTests
{
    [Fact]
    public void GetPacket_InputPacket_ReturnsMetadata()
    {
        var info = PacketsInfo.GetPacket(0x02);

        Assert.NotNull(info);
        Assert.Equal(0x02, info.Id);
        Assert.Equal("MoveRequest", info.Name);
        Assert.Equal(PacketDirectionType.Input, info.Direction);
        Assert.Equal(7, info.Size);
    }

    [Fact]
    public void GetPacket_OutputOnlyPacket_ReturnsMetadata()
    {
        var info = PacketsInfo.GetPacket(0x8C);

        Assert.NotNull(info);
        Assert.Equal("ConnectToGameServer", info.Name);
        Assert.Equal(PacketDirectionType.Output, info.Direction);
        Assert.Equal(11, info.Size);
    }

    [Fact]
    public void GetPacket_BidirectionalPacket_HasBothFlags()
    {
        var info = PacketsInfo.GetPacket(0x22);

        Assert.NotNull(info);
        Assert.Equal(PacketDirectionType.Input | PacketDirectionType.Output, info.Direction);
    }

    [Fact]
    public void GetPacket_VariablePacket_ReportsVariableSize()
    {
        var info = PacketsInfo.GetPacket(0xA9);

        Assert.NotNull(info);
        Assert.Equal(PacketLengths.Variable, info.Size);
    }

    [Fact]
    public void GetPacket_UnknownId_ReturnsNull()
    {
        Assert.Null(PacketsInfo.GetPacket(0xFF));
    }

    [Fact]
    public void Catalog_SizesAgreeWithPacketLengthsTable()
    {
        for (var id = 0; id < 256; id++)
        {
            var declared = PacketLengths.Get((byte)id);
            var info = PacketsInfo.GetPacket((byte)id);

            if (declared == PacketLengths.Unknown || info is null)
            {
                continue;
            }

            Assert.Equal(declared, info.Size);
        }
    }

    [Fact]
    public void Catalog_ContainsEveryPolDocumentedPacket()
    {
        // one entry per packet documented in https://docs.polserver.com/packets/index.php
        var count = 0;

        for (var id = 0; id < 256; id++)
        {
            if (PacketsInfo.GetPacket((byte)id) is not null)
            {
                count++;
            }
        }

        Assert.Equal(198, count);
    }

    [Fact]
    public void Catalog_EveryEntryHasNameAndDirection()
    {
        for (var id = 0; id < 256; id++)
        {
            var info = PacketsInfo.GetPacket((byte)id);

            if (info is null)
            {
                continue;
            }

            Assert.False(string.IsNullOrEmpty(info.Name));
            Assert.NotEqual(PacketDirectionType.None, info.Direction);
            Assert.Equal(id, info.Id);
        }
    }

    [Fact]
    public void Catalog_AndLengthsTable_CoverTheSameIds()
    {
        for (var id = 0; id < 256; id++)
        {
            var declared = PacketLengths.Get((byte)id);
            var info = PacketsInfo.GetPacket((byte)id);

            Assert.Equal(declared != PacketLengths.Unknown, info is not null);
        }
    }
}
