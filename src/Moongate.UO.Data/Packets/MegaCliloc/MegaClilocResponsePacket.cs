using System.Text;
using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.MegaCliloc;

namespace Moongate.UO.Data.Packets.MegaCliloc;

/// <summary>
/// MegaCliloc Response Packet (0xD6)
/// Sent by server to provide tooltip/cliloc information for requested objects
/// </summary>
public class MegaClilocResponsePacket : BaseUoPacket
{
    /// <summary>
    /// Collection of cliloc entries for different objects
    /// </summary>
    public List<MegaClilocEntry> Entries { get; set; } = new();

    public MegaClilocResponsePacket() : base(0xD6)
    {
    }


    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        /*
         *
         * BYTE[1] 0xD6
           BYTE[2] Length
           BYTE[2] 0x0001
           BYTE[4] Serial of item/creature
           BYTE[2] 0x0000
           BYTE[4] Serial of item/creature in all tests. This could be the serial of the item the entry to appear over.

           Loop of all the item/creature's properties to display in the order to display them. The name is always the first entry.
                   BYTE[4] Cliloc ID
                   BYTE[2] Length of (if any) Text to add into/with the cliloc
                   BYTE[?] Unicode text to be added into the cliloc. Not sent if Length of text above is 0

                   BYTE[4] 00000000 - Sent as end of packet/loop

         */
        /// Calculate total packet size
        var totalSize = CalculatePacketSize();
        /// Write packet header
        writer.Write(OpCode);
        writer.Write((short)totalSize);

        /// Write version/subcommand
        writer.Write((short)0x0001);

        /// Write each entry
        foreach (var entry in Entries)
        {
            var entryBuffer = WriteEntry(entry);

            writer.Write(entryBuffer.Span);
        }

        //
        /// End packet marker
        writer.Write(0x00000000);
        return writer.ToArray();
    }


    private ReadOnlyMemory<byte> WriteEntry(MegaClilocEntry entry)
    {
        using var writer = new SpanWriter(1, true);

        // Loop of all the item/creature's properties to display in the order to display them. The name is always the first entry.
        // BYTE[4] Cliloc ID
        // BYTE[2] Length of (if any) Text to add into/with the cliloc
        // BYTE[?] Unicode text to be added into the cliloc. Not sent if Length of text above is 0
        //
        // BYTE[4] 00000000 - Sent as end of packet/loop
        /// Write object serial
        writer.Write(entry.Serial.Value);

        /// Write unknown field (0x0000 in documentation)
        writer.Write((short)0x0000);

        /// Write object serial again (as per documentation)
        writer.Write(entry.Serial.Value);

        /// Write all properties for this object
        foreach (var property in entry.Properties)
        {
            /// Write cliloc ID
            writer.Write(property.ClilocId);

            /// Write text length
            if (string.IsNullOrEmpty(property.Text))
            {
                writer.Write((short)0);
            }
            else
            {
                /// Convert text to unicode bytes
                var textBytes = Encoding.Unicode.GetBytes(property.Text);
                writer.Write((short)textBytes.Length);
                writer.WriteUTF8(property.Text);
            }
        }

        return writer.ToArray();
    }

    private int CalculatePacketSize()
    {
        var size = 1 + 2 + 2; /// Header + Length + Version

        foreach (var entry in Entries)
        {
            size += 4 + 2 + 4; /// Serial + Unknown + Serial again

            foreach (var property in entry.Properties)
            {
                size += 4 + 2; /// Cliloc ID + Text Length

                if (!string.IsNullOrEmpty(property.Text))
                {
                    size += Encoding.Unicode.GetByteCount(property.Text);
                }
            }
        }

        size += 4; /// End marker

        return size;
    }
}
