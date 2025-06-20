using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Data;

namespace Moongate.UO.Data.Packets.Characters;

public class CharacterAfterDeletePacket : BaseUoPacket
{
    public List<CharacterEntry> Characters { get; set; } = new();

    public CharacterAfterDeletePacket() : base(0x86)
    {
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        var highSlot = -1;

        for (var i = Characters.Count - 1; i >= 0; i--)
        {
            if (Characters[i] != null)
            {
                highSlot = i;
                break;
            }
        }

        var count = Math.Max(Math.Max(highSlot + 1, 7), 5);
        var length = 4 + count * 60;
        writer.Write((byte)0x86); // Packet ID
        writer.Write((ushort)length);

        writer.Write((byte)count);

        for (int i = 0; i < count; i++)
        {
            var m = Characters[i];

            if (m == null)
            {
                writer.Clear(60);
            }
            else
            {
                writer.WriteAscii(m.Name, 30);
                writer.Clear(30); // password
            }
        }


        return writer.ToArray();
    }


    public void FillCharacters(List<CharacterEntry>? characters = null, int size = 7)
    {
        Characters.Clear();

        if (characters != null)
        {
            Characters.AddRange(characters);

            if (characters.Count < size)
            {
                for (var i = characters.Count; i < size; i++)
                {
                    Characters.Add(null);
                }
            }
        }
        else
        {
            for (var i = 0; i < size; i++)
            {
                Characters.Add(null);
            }
        }
    }
}
