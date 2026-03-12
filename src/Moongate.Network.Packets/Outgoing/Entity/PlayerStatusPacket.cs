using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x11, PacketSizing.Variable, Description = "Status Bar Info")]

/// <summary>
/// Represents PlayerStatusPacket.
/// </summary>
public class PlayerStatusPacket : BaseGameNetworkPacket
{
    public const byte ModernVersion = 6;
    public bool CanBeRenamed { get; set; }

    public ushort CurrentHits { get; set; }

    public ushort MaxHits { get; set; }

    public UOMobileEntity? Mobile { get; set; }

    public string Name { get; set; }

    public Serial Serial { get; set; }

    public byte Version { get; set; }

    public PlayerStatusPacket()
        : base(0x11) { }

    public PlayerStatusPacket(UOMobileEntity mobile, byte version = ModernVersion, bool canBeRenamed = false)
        : this()
    {
        Mobile = mobile;
        CanBeRenamed = canBeRenamed;
        Version = version;
    }

    public override void Write(ref SpanWriter writer)
    {
        if (Mobile is null)
        {
            throw new InvalidOperationException("Mobile must be set before writing PlayerStatusPacket.");
        }

        var maxHits = Mobile.MaxHits > 0 ? Mobile.MaxHits : Math.Max(1, Mobile.Hits);
        var currentHits = Math.Clamp(Mobile.Hits, 0, maxHits);

        writer.Write(OpCode);
        writer.Write((ushort)0); // Placeholder for length
        writer.Write(Mobile.Id.Value);
        writer.WriteAscii(Mobile.Name ?? string.Empty, 30);
        writer.WriteAttribute(maxHits, currentHits, true, true);
        writer.Write(CanBeRenamed);
        writer.Write(Version);

        if (Version >= 1)
        {
            writer.Write(Mobile.Gender == GenderType.Female);
            writer.Write((ushort)Mobile.EffectiveStrength);
            writer.Write((ushort)Mobile.EffectiveDexterity);
            writer.Write((ushort)Mobile.EffectiveIntelligence);
            writer.Write((ushort)Mobile.Stamina);
            writer.Write((ushort)Mobile.MaxStamina);
            writer.Write((ushort)Mobile.Mana);
            writer.Write((ushort)Mobile.MaxMana);
            writer.Write((uint)Math.Max(0, Mobile.Gold));
            writer.Write((ushort)Math.Clamp(Mobile.EffectivePhysicalResistance, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.Weight, 0, ushort.MaxValue));
        }

        if (Version >= 5)
        {
            writer.Write((ushort)Math.Clamp(Mobile.MaxWeight, 0, ushort.MaxValue));
            writer.Write((byte)(Mobile.RaceIndex + 1));
        }

        if (Version >= 3)
        {
            writer.Write((ushort)Math.Clamp(Mobile.StatCap, 0, ushort.MaxValue));
            writer.Write((byte)Math.Clamp(Mobile.Followers, byte.MinValue, byte.MaxValue));
            writer.Write((byte)Math.Clamp(Mobile.FollowersMax, byte.MinValue, byte.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveFireResistance, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveColdResistance, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectivePoisonResistance, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveEnergyResistance, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveLuck, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.MinWeaponDamage, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.MaxWeaponDamage, 0, ushort.MaxValue));
            writer.Write(Mobile.Tithing);
        }

        if (Version >= ModernVersion)
        {
            writer.Write((ushort)Math.Clamp(Mobile.ModifierCaps.PhysicalResist, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.ModifierCaps.FireResist, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.ModifierCaps.ColdResist, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.ModifierCaps.PoisonResist, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.ModifierCaps.EnergyResist, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveDefenseChanceIncrease, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.ModifierCaps.DefenseChanceIncrease, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveHitChanceIncrease, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveSwingSpeedIncrease, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveDamageIncrease, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveLowerReagentCost, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveSpellDamageIncrease, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveFasterCastRecovery, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveFasterCasting, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(Mobile.EffectiveLowerManaCost, 0, ushort.MaxValue));
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 42)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length)
        {
            return false;
        }

        Serial = (Serial)reader.ReadUInt32();
        Name = reader.ReadAscii(30);
        CurrentHits = reader.ReadUInt16();
        MaxHits = reader.ReadUInt16();
        CanBeRenamed = reader.ReadBoolean();
        Version = reader.ReadByte();

        if (Version >= 1)
        {
            if (reader.Remaining < 24)
            {
                return false;
            }

            _ = reader.ReadBoolean(); // Sex+Race
            _ = reader.ReadUInt16();  // Strength
            _ = reader.ReadUInt16();  // Dexterity
            _ = reader.ReadUInt16();  // Intelligence
            _ = reader.ReadUInt16();  // Current Stamina
            _ = reader.ReadUInt16();  // Max Stamina
            _ = reader.ReadUInt16();  // Current Mana
            _ = reader.ReadUInt16();  // Max Mana
            _ = reader.ReadUInt32();  // Gold
            _ = reader.ReadUInt16();  // Resistance
            _ = reader.ReadUInt16();  // Weight
        }

        if (Version >= 5)
        {
            if (reader.Remaining < 3)
            {
                return false;
            }

            _ = reader.ReadUInt16(); // Max weight
            _ = reader.ReadByte();   // Race
        }

        if (Version >= 3)
        {
            if (reader.Remaining < 18)
            {
                return false;
            }

            _ = reader.ReadUInt16(); // Stat cap
            _ = reader.ReadByte();   // Followers
            _ = reader.ReadByte();   // Followers max
            _ = reader.ReadUInt16(); // Fire resist
            _ = reader.ReadUInt16(); // Cold resist
            _ = reader.ReadUInt16(); // Poison resist
            _ = reader.ReadUInt16(); // Energy resist
            _ = reader.ReadUInt16(); // Luck
            _ = reader.ReadUInt16(); // Min damage
            _ = reader.ReadUInt16(); // Max damage
            _ = reader.ReadInt32();  // Tithing
        }

        if (Version >= ModernVersion)
        {
            if (reader.Remaining < 30)
            {
                return false;
            }

            _ = reader.ReadUInt16(); // Physical resist cap
            _ = reader.ReadUInt16(); // Fire resist cap
            _ = reader.ReadUInt16(); // Cold resist cap
            _ = reader.ReadUInt16(); // Poison resist cap
            _ = reader.ReadUInt16(); // Energy resist cap
            _ = reader.ReadUInt16(); // Defense chance increase
            _ = reader.ReadUInt16(); // Defense chance cap
            _ = reader.ReadUInt16(); // Hit chance increase
            _ = reader.ReadUInt16(); // Swing speed increase
            _ = reader.ReadUInt16(); // Damage increase
            _ = reader.ReadUInt16(); // Lower reagent cost
            _ = reader.ReadUInt16(); // Spell damage increase
            _ = reader.ReadUInt16(); // Faster cast recovery
            _ = reader.ReadUInt16(); // Faster casting
            _ = reader.ReadUInt16(); // Lower mana cost
        }

        return reader.Remaining == 0;
    }
}
