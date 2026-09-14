using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class LoginSeedPacket : IIncomingPacket<LoginSeedPacket>
{
    private const byte PacketOpCode = 0xEF;
    private const int PacketLength = 21;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;
    public uint Seed { get; }
    public uint Major { get; }
    public uint Minor { get; }
    public uint Revision { get; }
    public uint Patch { get; }

    public LoginSeedPacket(uint seed, uint major, uint minor, uint revision, uint patch)
    {
        Seed = seed;
        Major = major;
        Minor = minor;
        Revision = revision;
        Patch = patch;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out LoginSeedPacket? packet)
    {
        packet = null;
        if (!PacketValidation.HasFixedHeader(data, PacketOpCode, PacketLength))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadUInt32BigEndian(out var seed)
            || !reader.TryReadUInt32BigEndian(out var major)
            || !reader.TryReadUInt32BigEndian(out var minor)
            || !reader.TryReadUInt32BigEndian(out var revision)
            || !reader.TryReadUInt32BigEndian(out var patch))
        {
            return false;
        }

        packet = new LoginSeedPacket(seed, major, minor, revision, patch);
        return true;
    }
}
