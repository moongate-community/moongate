using System.Buffers.Binary;
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
        if (reader.Remaining < 2)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();
        var payloadLength = declaredLength - 3;

        if (declaredLength < 5 || payloadLength != reader.Remaining)
        {
            return false;
        }

        var payload = reader.ReadBytes(payloadLength);
        var payloadReader = new SpanReader(payload);

        try
        {
            if (payloadLength == 4)
            {
                AdvertisedClientType = payloadReader.ReadUInt32();
                VersionString = string.Empty;
            }
            else if (payloadLength >= 4)
            {
                var firstField = BinaryPrimitives.ReadUInt16BigEndian(payload);

                if (payloadLength > 4)
                {
                    var secondField = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2));

                    if (secondField is 0x02 or 0x03)
                    {
                        _ = payloadReader.ReadUInt16();
                        AdvertisedClientType = payloadReader.ReadUInt16();
                        VersionString = payloadReader.ReadAscii(payloadLength - 4).TrimEnd('\0').Trim();
                    }
                    else if (firstField is 0x02 or 0x03)
                    {
                        AdvertisedClientType = payloadReader.ReadUInt16();
                        VersionString = payloadReader.ReadAscii(payloadLength - 2).TrimEnd('\0').Trim();
                    }
                    else if (payloadLength == 6)
                    {
                        _ = payloadReader.ReadUInt16();
                        AdvertisedClientType = payloadReader.ReadUInt32();
                        VersionString = string.Empty;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }
        finally
        {
            payloadReader.Dispose();
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
