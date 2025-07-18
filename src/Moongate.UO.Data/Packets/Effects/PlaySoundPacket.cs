using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Interfaces.Entities;

namespace Moongate.UO.Data.Packets.Effects;

public class PlaySoundPacket : BaseUoPacket
{
    public IPositionEntity Entity { get; set; }
    public ushort SoundId { get; set; }
    public bool IsLoop { get; set; }

    public PlaySoundPacket() : base(0x54)
    {
    }

    public PlaySoundPacket(IPositionEntity entity, ushort soundId, bool isLoop = false) : this()
    {
        Entity = entity;
        SoundId = soundId;
        IsLoop = isLoop;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(IsLoop ? (byte)0x00 : (byte)0x01); // Mode: 0x00 for loop, 0x01 for single sound
        writer.Write(SoundId);                          // Sound ID
        writer.Write((ushort)0);
        writer.Write((short)Entity.Location.X);
        writer.Write((short)Entity.Location.Y);
        writer.Write((short)Entity.Location.Z);
        return writer.ToArray();
    }
}
