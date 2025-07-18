using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.Effects;

public class PlayMusicPacket  : BaseUoPacket
{
    public int MusicId { get; set; }

    public PlayMusicPacket() : base(0x6D)
    {
    }

    public PlayMusicPacket(int musicId) : this()
    {
        MusicId = musicId;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((short)MusicId);

        return writer.ToArray();
    }
}
