using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Incoming.Login;

[PacketHandler(0xEF, PacketSizing.Fixed, Length = 21)]
public sealed class LoginSeedPacket : BaseFixedPacket<LoginSeedPacket>, IIncomingPacket<LoginSeedPacket>
{
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

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt32BigEndian(out var seed) ||
            !reader.TryReadUInt32BigEndian(out var major) ||
            !reader.TryReadUInt32BigEndian(out var minor) ||
            !reader.TryReadUInt32BigEndian(out var revision) ||
            !reader.TryReadUInt32BigEndian(out var patch))
        {
            return false;
        }

        packet = new(seed, major, minor, revision, patch);

        return true;
    }
}
