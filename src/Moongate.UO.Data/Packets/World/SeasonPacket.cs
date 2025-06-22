using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Environment;

public class SeasonPacket : BaseUoPacket
{
    public bool PlaySounds { get; set; }
    public SeasonType Season { get; set; }

    public SeasonPacket() : base(0xBC)
    {
    }

    public SeasonPacket(SeasonType season, bool playingSounds = true) : this()
    {
        Season = season;
        PlaySounds = playingSounds;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)Season);
        writer.Write(PlaySounds);


        return writer.ToArray();
    }
}
