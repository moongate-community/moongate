using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;

namespace Moongate.UO.Data.Packets.GeneralInformation.SubCommands;

/// <summary>
/// Use Targeted Item subcommand data (0x2C)
/// </summary>
public sealed class UseTargetedItemData : ISubcommandData
{
    /// <summary>Item serial</summary>
    public uint ItemSerial { get; set; }

    /// <summary>Target serial</summary>
    public uint TargetSerial { get; set; }

    /// <inheritdoc />
    public int Length => 8;

    /// <inheritdoc />
    public void Read(SpanReader reader)
    {
        ItemSerial = reader.ReadUInt32();
        TargetSerial = reader.ReadUInt32();
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(ItemSerial);
        writer.Write(TargetSerial);

        return writer.ToArray();
    }
}
