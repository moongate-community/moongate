using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Mobiles;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The status bar of the player's own character (0x11), version 5 (Mondain's Legacy, 91 bytes): name, hits,
///     stats, gold, weight, race, followers, resistances, luck, damage and tithing. Compact, for another mobile, it is
///     version 0 (43 bytes): the name and the hit points as a share of 100.
/// </summary>
[PacketHandler(0x11, PacketSizing.Variable, MinimumLength = 43)]
public sealed class MobileStatusPacket : BasePacket<MobileStatusPacket>, IOutgoingPacket
{
    private const byte Version = 5;
    private const byte CompactVersion = 0;
    private const int NameLength = 30;
    private const int Share = 100;

    public override int Length => Compact ? 43 : 91;

    /// <summary>
    ///     Gets whether only the name and the health bar are sent, as for a mobile that is not the player's own.
    /// </summary>
    public bool Compact { get; }

    public MobileStatusInfo Status { get; }

    public MobileStatusPacket(MobileStatusInfo status, bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(status);
        Status = status;
        Compact = compact;
    }

    public void Write(ref PacketWriter writer)
    {
        var status = Status;
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(status.Serial);
        writer.WriteFixedAscii(status.Name.Length > NameLength ? status.Name[..NameLength] : status.Name, NameLength);

        if (Compact)
        {
            // As ModernUO: never the real numbers of another mobile.
            writer.WriteUInt16BigEndian((ushort)(status.HitsMax == 0 ? status.Hits : status.Hits * Share / status.HitsMax));
            writer.WriteUInt16BigEndian((ushort)(status.HitsMax == 0 ? 0 : Share));
            writer.WriteByte(status.CanBeRenamed ? (byte)1 : (byte)0);
            writer.WriteByte(CompactVersion);

            return;
        }

        writer.WriteUInt16BigEndian((ushort)status.Hits);
        writer.WriteUInt16BigEndian((ushort)status.HitsMax);
        writer.WriteByte(status.CanBeRenamed ? (byte)1 : (byte)0);
        writer.WriteByte(Version);
        writer.WriteByte(status.Female ? (byte)1 : (byte)0);
        writer.WriteUInt16BigEndian((ushort)status.Strength);
        writer.WriteUInt16BigEndian((ushort)status.Dexterity);
        writer.WriteUInt16BigEndian((ushort)status.Intelligence);
        writer.WriteUInt16BigEndian((ushort)status.Stamina);
        writer.WriteUInt16BigEndian((ushort)status.StaminaMax);
        writer.WriteUInt16BigEndian((ushort)status.Mana);
        writer.WriteUInt16BigEndian((ushort)status.ManaMax);
        writer.WriteUInt32BigEndian((uint)status.Gold);
        writer.WriteUInt16BigEndian((ushort)status.PhysicalResistance);
        writer.WriteUInt16BigEndian((ushort)status.Weight);
        writer.WriteUInt16BigEndian((ushort)status.MaxWeight);

        // The client counts races from 1: human is 1, elf 2, gargoyle 3.
        writer.WriteByte((byte)((int)status.Race + 1));
        writer.WriteUInt16BigEndian((ushort)status.StatCap);
        writer.WriteByte((byte)status.Followers);
        writer.WriteByte((byte)status.FollowersMax);
        writer.WriteUInt16BigEndian((ushort)status.FireResistance);
        writer.WriteUInt16BigEndian((ushort)status.ColdResistance);
        writer.WriteUInt16BigEndian((ushort)status.PoisonResistance);
        writer.WriteUInt16BigEndian((ushort)status.EnergyResistance);
        writer.WriteUInt16BigEndian((ushort)status.Luck);
        writer.WriteUInt16BigEndian((ushort)status.DamageMin);
        writer.WriteUInt16BigEndian((ushort)status.DamageMax);
        writer.WriteUInt32BigEndian((uint)status.TithingPoints);
    }
}
