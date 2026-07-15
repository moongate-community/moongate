using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Status bar info (0x11): the player's own status window. Written in the High Seas layout
/// (version 6, 121 bytes) that modern 7.x clients expect. Combat-derived figures Moongate does not
/// model yet (resists, luck, weapon damage, tithing, gold, weight) are sent as zero. Back-patched.
/// </summary>
public readonly record struct MobileStatusPacket(
    Serial Serial,
    string Name,
    ushort Hits,
    ushort HitsMax,
    bool Female,
    ushort Strength,
    ushort Dexterity,
    ushort Intelligence,
    ushort Stamina,
    ushort StaminaMax,
    ushort Mana,
    ushort ManaMax,
    RaceType Race,
    ushort StatCap,
    byte FollowersMax
) : IOutgoingPacket
{
    public const byte PacketId = 0x11;

    private const byte Version = 6;
    private const ushort Length = 121; // High Seas (version 6) status layout

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Length);
        writer.Write(Serial);
        writer.WriteAscii(Name, 30);

        writer.Write((short)Hits);
        writer.Write((short)HitsMax);

        writer.Write((byte)0);    // name-change flag
        writer.Write(Version);

        writer.Write((byte)(Female ? 1 : 0));

        writer.Write((short)Strength);
        writer.Write((short)Dexterity);
        writer.Write((short)Intelligence);

        writer.Write((short)Stamina);
        writer.Write((short)StaminaMax);
        writer.Write((short)Mana);
        writer.Write((short)ManaMax);

        writer.Write(0);          // gold
        writer.Write((short)0);   // physical resistance / armor rating
        writer.Write((short)0);   // weight

        writer.Write((short)0);   // max weight (version >= 5)
        writer.Write((byte)((byte)Race + 1)); // race id, 1-based (version >= 5)

        writer.Write((short)StatCap);

        writer.Write((byte)0);    // followers
        writer.Write(FollowersMax);

        writer.Write((short)0);   // fire resistance (version >= 4)
        writer.Write((short)0);   // cold resistance
        writer.Write((short)0);   // poison resistance
        writer.Write((short)0);   // energy resistance
        writer.Write((short)0);   // luck
        writer.Write((short)0);   // damage min
        writer.Write((short)0);   // damage max
        writer.Write(0);          // tithing points

        for (var i = 0; i < 15; i++)
        {
            writer.Write((short)0); // extended AOS statuses (version >= 6)
        }
    }
}
