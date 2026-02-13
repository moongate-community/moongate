using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.World;

/// <summary>
/// OpCode 0x6D - Set Music Packet (Play Music)
/// Consolidates: SetMusicPacket (by enum) and PlayMusicPacket (by int)
/// Sends music ID to client
/// </summary>
public class SetMusicPacket : BaseUoPacket
{
    public int MusicId { get; set; }

    public SetMusicPacket() : base(0x6D) { }

    /// <summary>Constructor with int music ID</summary>
    public SetMusicPacket(int musicId) : this()
        => MusicId = musicId;

    /// <summary>Constructor with MusicName enum (for type safety)</summary>
    public SetMusicPacket(MusicName music) : this()
        => MusicId = (int)music;

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)MusicId);

        return writer.ToArray();
    }
}
