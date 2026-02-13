using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.World;

public class SetMusicPacket : BaseUoPacket
{
    public MusicName Music { get; set; }

    public SetMusicPacket() : base(0x6D) { }

    public SetMusicPacket(MusicName music) : this()
        => Music = music;

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)Music);

        return writer.ToArray();
    }
}
