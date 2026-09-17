using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Internal.Login;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Incoming.Login;

[PacketHandler(0xBD, PacketSizing.Variable, MinimumLength = 4)]
public sealed class ClientVersionPacket : BasePacket<ClientVersionPacket>, IIncomingPacket<ClientVersionPacket>
{
    public override int Length { get; }
    public string Version { get; }

    public ClientVersionPacket(string version)
        : this(version, GetCanonicalLength(version))
    {
    }

    private ClientVersionPacket(string version, int length)
    {
        PacketValidation.ValidateAscii(version, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

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
        if (!HasValidHeader(data))
        {
            return false;
        }

        var payload = data[LoginProtocolConstants.VariableHeaderLength..];
        var reader = new PacketReader(payload);
        var parsed = payload[^1] == 0
                         ? reader.TryReadNullTerminatedAscii(payload.Length, out var version)
                         : reader.TryReadAscii(payload.Length, out version);
        if (!parsed || string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        packet = new ClientVersionPacket(version, data.Length);
        return true;
    }

    private static int GetCanonicalLength(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return checked(LoginProtocolConstants.VariableHeaderLength + version.Length);
    }
}
