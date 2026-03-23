using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Version;

namespace Moongate.Network.Packets.Incoming.Login;

[PacketHandler(0xE1, PacketSizing.Variable, Description = "Client Type (KR/SA)")]

/// <summary>
/// Represents ClientTypePacket.
/// </summary>
public class ClientTypePacket : BaseGameNetworkPacket
{
    public uint AdvertisedClientType { get; private set; }

    public ClientType ResolvedClientType { get; private set; } = ClientType.Classic;

    public string VersionString { get; private set; } = string.Empty;

    public ClientTypePacket()
        : base(0xE1) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 4)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength < 5)
        {
            return false;
        }

        if (reader.Remaining > 6)
        {
            AdvertisedClientType = reader.ReadUInt16();
            VersionString = reader.Remaining == 0 ? string.Empty : reader.ReadAscii(reader.Remaining).TrimEnd('\0').Trim();
        }
        else
        {
            if (reader.Remaining < 6)
            {
                return false;
            }

            _ = reader.ReadUInt16();
            AdvertisedClientType = reader.ReadUInt32();
            VersionString = string.Empty;
        }

        ResolvedClientType = AdvertisedClientType switch
        {
            0x02 => ClientType.KR,
            0x03 => ClientType.SA,
            _    => ClientType.Classic
        };

        return true;
    }
}
