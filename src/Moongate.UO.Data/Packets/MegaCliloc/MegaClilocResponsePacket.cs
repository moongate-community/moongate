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
            WriteEntry(writer, entry);
        }

        //
        /// End packet marker
        writer.Write(0x00000000);
        return writer.ToArray();
    }


    private void WriteEntry(SpanWriter writer, MegaClilocEntry entry)
    {
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
                writer.Write(textBytes);
            }
        }
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
