using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class ClientVersionPacket : IIncomingPacket<ClientVersionPacket>
{
    private const byte PacketOpCode = 0xBD;
    private const int HeaderLength = 3;
    private const int MinimumPacketLength = 4;

    public byte OpCode => PacketOpCode;
    public int Length { get; }
    public string Version { get; }

    public ClientVersionPacket(string version)
        : this(version, GetCanonicalLength(version))
    {
    }

    private ClientVersionPacket(string version, int length)
    {
        PacketValidation.ValidateAscii(version, nameof(version));
        if (version.Length == 0)
        {
            throw new ArgumentException("The client version must not be empty.", nameof(version));
        }

        if (length > ushort.MaxValue)
        {
            throw new ArgumentException("The client version packet length must fit UInt16.", nameof(version));
        }

        Version = version;
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ClientVersionPacket? packet)
    {
        packet = null;
        if (!PacketValidation.HasVariableHeader(data, PacketOpCode, MinimumPacketLength))
        {
            return false;
        }

        var payload = data[HeaderLength..];
        var reader = new PacketReader(payload);
        var parsed = payload[^1] == 0
                         ? reader.TryReadNullTerminatedAscii(payload.Length, out var version)
                         : reader.TryReadAscii(payload.Length, out version);
        if (!parsed || string.IsNullOrEmpty(version))
        {
            return false;
        }

        packet = new ClientVersionPacket(version, data.Length);
        return true;
    }

    private static int GetCanonicalLength(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return checked(HeaderLength + version.Length);
    }
}
