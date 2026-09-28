using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Mobiles;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The status bar of the player's own character (0x11), version 5 (Mondain's Legacy, 91 bytes): name, hits,
///     stats, gold, weight, race, followers, resistances, luck, damage and tithing.
/// </summary>
[PacketHandler(0x11, PacketSizing.Variable, MinimumLength = 43)]
public sealed class MobileStatusPacket : BasePacket<MobileStatusPacket>, IOutgoingPacket
{
    private const byte Version = 5;
    private const int NameLength = 30;

    public override int Length => 91;

    public MobileStatusInfo Status { get; }

    public MobileStatusPacket(MobileStatusInfo status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Status = status;
    }

    public void Write(ref PacketWriter writer)
    {
        var status = Status;
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(status.Serial);
        writer.WriteFixedAscii(status.Name.Length > NameLength ? status.Name[..NameLength] : status.Name, NameLength);
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
