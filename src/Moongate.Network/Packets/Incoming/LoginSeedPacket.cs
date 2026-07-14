using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Login seed (0xEF): connection seed and client version, sent first by ClassicUO.</summary>
public readonly record struct LoginSeedPacket(uint Seed, uint Major, uint Minor, uint Revision, uint Prototype)
    : IIncomingPacket<LoginSeedPacket>
{
    public static byte PacketId => 0xEF;

    public static LoginSeedPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        var seed = reader.ReadUInt32();
        var major = reader.ReadUInt32();
        var minor = reader.ReadUInt32();
        var revision = reader.ReadUInt32();
        var prototype = reader.ReadUInt32();

        return new(seed, major, minor, revision, prototype);
    }
}
