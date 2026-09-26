using System.Collections.ObjectModel;
using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Server.Ultima.Packets.Characters;

/// <summary>
///     Sends the character slots of an account and the starting cities, using the layout of clients 7.0.13.0 and
///     later: the city entries carry coordinates, map id and cliloc, and the packet ends with the last used slot.
/// </summary>
[PacketHandler(0xA9, PacketSizing.Variable, MinimumLength = 6)]
public sealed class CharacterListPacket : BasePacket<CharacterListPacket>, IOutgoingPacket
{
    /// <summary>
    ///     Opcode, length, slot count, city count, flags and last slot: everything except slots and cities.
    /// </summary>
    private const int FixedLength = 11;

    private const int SlotNameLength = 30;
    private const int SlotEntryLength = 60;
    private const int CityEntryLength = 89;
    private const ushort NoLastCharacterSlot = 0xFFFF;

    private static readonly int[] SupportedSlotCounts = [1, 5, 6, 7];

    /// <summary>
    ///     The most ASCII characters a city town or description can have.
    /// </summary>
    public const int CityTextLength = 32;

    /// <summary>
    ///     The most cities the list can carry: the count is one byte.
    /// </summary>
    public const int MaximumCityCount = byte.MaxValue;

    public override int Length { get; }

    /// <summary>
    ///     One entry per slot; a null or empty name is an empty slot.
    /// </summary>
    public IReadOnlyList<string?> Characters { get; }

    public IReadOnlyList<StartingCityContent> Cities { get; }

    public CharacterListFlags Flags { get; }

    public CharacterListPacket(
        IEnumerable<string?> characters,
        IEnumerable<StartingCityContent> cities,
        CharacterListFlags flags
    )
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(cities);

        var slotSnapshot = characters.ToArray();
        var citySnapshot = cities.ToArray();

        if (!SupportedSlotCounts.Contains(slotSnapshot.Length))
        {
            throw new ArgumentException(
                $"The client supports 1, 5, 6 or 7 character slots, got {slotSnapshot.Length}.",
                nameof(characters)
            );
        }

        foreach (var name in slotSnapshot.Where(name => name is not null))
        {
            ValidateText(name!, SlotNameLength, nameof(characters));
        }

        if (citySnapshot.Length > MaximumCityCount)
        {
            throw new ArgumentException(
                $"The city list cannot contain more than {MaximumCityCount} entries.",
                nameof(cities)
            );
        }

        foreach (var city in citySnapshot)
        {
            if (city is null)
            {
                throw new ArgumentException("The city list must not contain null entries.", nameof(cities));
            }

            ValidateText(city.Town, CityTextLength, nameof(cities));
            ValidateText(city.Description, CityTextLength, nameof(cities));
        }

        Length = FixedLength + SlotEntryLength * slotSnapshot.Length + CityEntryLength * citySnapshot.Length;
        Characters = new ReadOnlyCollection<string?>(slotSnapshot);
        Cities = new ReadOnlyCollection<StartingCityContent>(citySnapshot);
        Flags = flags;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteByte((byte)Characters.Count);

        foreach (var name in Characters)
        {
            writer.WriteFixedAscii(name ?? string.Empty, SlotNameLength);

            // Unused password field.
            writer.WriteFixedAscii(string.Empty, SlotEntryLength - SlotNameLength);
        }

        writer.WriteByte((byte)Cities.Count);

        for (var index = 0; index < Cities.Count; index++)
        {
            var city = Cities[index];

            writer.WriteByte((byte)index);
            writer.WriteFixedAscii(city.Town, CityTextLength);
            writer.WriteFixedAscii(city.Description, CityTextLength);
            writer.WriteUInt32BigEndian(unchecked((uint)city.Location.X));
            writer.WriteUInt32BigEndian(unchecked((uint)city.Location.Y));
            writer.WriteUInt32BigEndian(unchecked((uint)city.Location.Z));
            writer.WriteUInt32BigEndian((uint)city.Map);
            writer.WriteUInt32BigEndian(city.Cliloc.Value);
            writer.WriteUInt32BigEndian(0);
        }

        writer.WriteUInt32BigEndian((uint)Flags);
        writer.WriteUInt16BigEndian(NoLastCharacterSlot);
    }

    private static void ValidateText(string? value, int maximumLength, string parameterName)
    {
        if (value is null || value.Length > maximumLength || !Ascii.IsValid(value))
        {
            throw new ArgumentException(
                $"The text must be ASCII, not null and at most {maximumLength} characters.",
                parameterName
            );
        }
    }
}
