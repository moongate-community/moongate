using System.Net;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Internal.Login;

namespace Moongate.Network.Packets.Data.Login;

public sealed class GameServerEntry
{
    private readonly byte[] _addressBytes;

    public ushort ServerIndex { get; }
    public string Name { get; }
    public byte FullPercent { get; }
    public sbyte TimeZone { get; }
    public IPAddress Address => new(_addressBytes);

    public GameServerEntry(
        ushort serverIndex,
        string name,
        byte fullPercent,
        sbyte timeZone,
        IPAddress address
    )
    {
        PacketValidation.ValidateFixedAscii(name, LoginProtocolConstants.ServerNameLength, nameof(name));
        _addressBytes = PacketValidation.SnapshotIPv4(address, nameof(address));
        ServerIndex = serverIndex;
        Name = name;
        FullPercent = fullPercent;
        TimeZone = timeZone;
    }

    internal ReadOnlySpan<byte> GetAddressBytes()
    {
        return _addressBytes;
    }
}
