using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.ContextMenus;

namespace Moongate.Tests.Server.Ultima.Packets.ContextMenus;

public sealed class DisplayContextMenuPacketTests
{
    // ServUO's DisplayContextMenu, the format of clients 6.0.0.0 and later: sub-command 0x14, format 2, the
    // target, the count, then for each entry its cliloc whole, its index and its flags.
    [Fact]
    public void Encode_IsTheMenuOfTheTarget_AnEntryOutOfReachGreyedOut()
    {
        var packet = new DisplayContextMenuPacket(new Serial(0x00000100), [(3006123, false), (3006105, true)]);

        Assert.Equal(
            Convert.FromHexString(
                "BF" + "001C" + "0014" + "0002" + "00000100" + "02" +
                "002DDEAB" + "0000" + "0000" +
                "002DDE99" + "0001" + "0001"
            ),
            PacketCodec.Encode(packet)
        );
    }

    [Fact]
    public void Encode_MoreEntriesThanAByteCounts_IsRefused()
    {
        var entries = Enumerable.Repeat((3006123, false), 256).ToArray();

        Assert.Throws<ArgumentException>(() => new DisplayContextMenuPacket(new Serial(0x100), entries));
    }
}
