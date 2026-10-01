using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class PlayMusicPacketTests
{
    [Theory, InlineData(MusicType.Create1, "6D0001"), InlineData(MusicType.NoMusic, "6D1FFF")]
    public void Encode_WritesTheTrack(MusicType music, string expected)
    {
        Assert.Equal(Convert.FromHexString(expected), PacketCodec.Encode(new PlayMusicPacket(music)));
    }
}
