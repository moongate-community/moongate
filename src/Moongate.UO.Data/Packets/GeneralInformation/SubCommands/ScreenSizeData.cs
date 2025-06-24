using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;

namespace Moongate.UO.Data.Packets.GeneralInformation.SubCommands;

/// <summary>
/// Screen Size subcommand data (0x05)
/// </summary>
public sealed class ScreenSizeData : ISubcommandData
{
    /// <summary>Unknown field, always 0</summary>
    public ushort Unknown1 { get; set; }

    /// <summary>Screen width</summary>
    public ushort Width { get; set; }

    /// <summary>Screen height</summary>
    public ushort Height { get; set; }

    /// <summary>Unknown field</summary>
    public ushort Unknown2 { get; set; }

    /// <inheritdoc />
    public int Length => 8;

    /// <inheritdoc />
    public void Read(SpanReader reader)
    {
        Unknown1 = reader.ReadUInt16();
        Width = reader.ReadUInt16();
        Height = reader.ReadUInt16();
        Unknown2 = reader.ReadUInt16();
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(Unknown1);
        writer.Write(Width);
        writer.Write(Height);
        writer.Write(Unknown2);

        return writer.ToArray();
    }
}
