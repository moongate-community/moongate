using System.Text;
using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.MegaCliloc;

/// <summary>
/// MegaCliloc Response Packet (0xD6)
/// Sent by server to provide tooltip/cliloc information for requested objects
/// NOTE: This packet handles ONE object at a time, not multiple objects!
/// </summary>
public class MegaClilocResponsePacket : BaseUoPacket
{
    /// <summary>
    /// Serial of the object this packet is for
    /// </summary>
    public Serial Serial { get; set; }

    /// <summary>
    /// List of properties/clilocs for this specific object
    /// </summary>
    public List<MegaClilocProperty> Properties { get; set; } = new();

    public MegaClilocResponsePacket() : base(0xD6) { }

    public MegaClilocResponsePacket(Serial serial) : base(0xD6)
        => Serial = serial;

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        /*
         * BYTE[1] 0xD6
         * BYTE[2] Length
         * BYTE[2] 0x0001
         * BYTE[4] Serial of item/creature
         * BYTE[2] 0x0000
         * BYTE[4] Serial of item/creature (repeated)
         * Loop of all the item/creature's properties to display in the order to display them. The name is always the first entry.
         *     BYTE[4] Cliloc ID
         *     BYTE[2] Length of (if any) Text to add into/with the cliloc
         *     BYTE[?] Unicode text to be added into the cliloc. Not sent if Length of text above is 0
         * BYTE[4] 00000000 - Sent as end of packet/loop
         */

        /// Calculate total packet size
        var totalSize = CalculatePacketSize();

        /// Write packet header
        writer.Write(OpCode);
        writer.Write((short)totalSize);

        /// Write version/subcommand
        writer.Write((short)0x0001);

        /// Write object serial
        writer.Write(Serial.Value);

        /// Write unknown field (0x0000 in documentation)
        writer.Write((short)0x0000);

        /// Write object serial again (as per documentation)
        writer.Write(Serial.Value);

        /// Write all properties for this object
        foreach (var property in Properties)
        {
            /// Write cliloc ID
            writer.Write(property.ClilocId);

            /// Write text length and text
            if (string.IsNullOrEmpty(property.Text))
            {
                writer.Write((short)0);
            }
            else
            {
                var textBytes = Encoding.Unicode.GetBytes(property.Text);
                writer.Write((short)textBytes.Length);
                writer.WriteLittleUni(property.Text);
            }
        }

        /// End packet marker
        writer.Write(0x00000000);

        return writer.ToArray();
    }

    private int CalculatePacketSize()
    {
        var size = 1 + 2 + 2 + 4 + 2 + 4; /// Header + Length + Version + Serial + Unknown + Serial again

        foreach (var property in Properties)
        {
            size += 4 + 2; /// Cliloc ID + Text Length

            if (!string.IsNullOrEmpty(property.Text))
            {
                size += Encoding.Unicode.GetByteCount(property.Text);
            }
        }

        size += 4; /// End marker

        return size;
    }
}
