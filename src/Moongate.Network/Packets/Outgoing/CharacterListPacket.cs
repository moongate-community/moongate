using Moongate.Network.Interfaces;
using Moongate.UO.Data.StartingCities;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Character list (0xA9): the character slots (empty until characters are persisted) followed by the
/// starting cities, in the extended 7.0.13+ layout. Length is <c>11 + 60*SlotCount + 89*Cities.Count</c>.
/// </summary>
public readonly record struct CharacterListPacket(
    IReadOnlyList<StartingCity> Cities,
    byte SlotCount,
    CharacterListFlagType Flags
) : IOutgoingPacket
{
    public const byte PacketId = 0xA9;

    private const int CityTextLength = 32;

    public void Write(ref SpanWriter writer)
    {
        var length = 11 + (60 * SlotCount) + (89 * Cities.Count);

        writer.Write(PacketId);
        writer.Write((ushort)length);
        writer.Write(SlotCount);

        for (var i = 0; i < SlotCount; i++)
        {
            writer.WriteAscii(string.Empty, 30); // name
            writer.WriteAscii(string.Empty, 30); // password
        }

        writer.Write((byte)Cities.Count);

        for (var i = 0; i < Cities.Count; i++)
        {
            var city = Cities[i];

            writer.Write((byte)i);
            writer.WriteAscii(city.City, CityTextLength);
            writer.WriteAscii(city.Building, CityTextLength);
            writer.Write(city.X);
            writer.Write(city.Y);
            writer.Write(city.Z);
            writer.Write((int)city.Map);
            writer.Write(city.Description);
            writer.Write(0);
        }

        writer.Write((int)Flags);
        writer.Write((short)-1);
    }
}
