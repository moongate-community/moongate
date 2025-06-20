using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class CharactersStartingLocationsPacket : BaseUoPacket
{
    public List<CityInfo> Cities { get; set; } = new List<CityInfo>();

    public List<CharacterEntry> Characters { get; } = new();


    public void FillCharacters(List<CharacterEntry>? characters = null, int size = 7)
    {
        Characters.Clear();

        if (characters != null)
        {
            Characters.AddRange(characters);
        }
        else
        {
            for (var i = 0; i < size; i++)
            {
                Characters.Add(null);
            }
        }
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        //var client70130 = true;
        //var textLength = 32;

        var highSlot = -1;

        for (var i = Characters.Count - 1; i >= 0; i--)
        {
            if (Characters[i] != null)
            {
                highSlot = i;
                break;
            }
        }

        // Supported values are 1, 5, 6, or 7
        var count = Math.Max(highSlot + 1, 7);
        if (count is not 1 and < 5)
        {
            count = 5;
        }

        var length = (11 + (32 * 2 + 25) * Cities.Count) + count * 60;

        // var length =
        //     (client70130 ? 11 + (textLength * 2 + 25) * Cities.Count() : 9 + (textLength * 2 + 1) * Cities.Count()) +
        //    count * 60;


        writer.Write(OpCode);
        writer.Write((ushort)length);
        writer.Write((byte)count);

        foreach (var character in Characters)
        {
            if (character == null)
            {
                writer.Clear(60);
                continue;
            }

            writer.WriteAscii(character.Name, 30);
            writer.Clear(30);
        }

        writer.Write((byte)Cities.Count);

        for (int i = 0; i < Cities.Count; ++i)
        {
            var ci = Cities[i];

            writer.Write((byte)i);
            writer.WriteAscii(ci.City, 32);
            writer.WriteAscii(ci.Building, 32);
            //  if (client70130)
            // {
            writer.Write(ci.X);
            writer.Write(ci.Y);
            writer.Write(ci.Z);
            writer.Write(ci.Map?.MapID ?? 0);
            writer.Write(ci.Description);
            writer.Write(0);
            // }
        }

        var flags = ExpansionInfo.CoreExpansion.CharacterListFlags;
        flags |= CharacterListFlags.SixthCharacterSlot | CharacterListFlags.SeventhCharacterSlot;
        writer.Write((int)flags);
        writer.Write((short)-1);

        return writer.ToArray();
    }
    // }

    public CharactersStartingLocationsPacket() : base(0xA9)
    {
    }
}
