using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;

namespace Moongate.UO.Data.Packets.GeneralInformation.SubCommands;

/// <summary>
/// Closed Status Gump subcommand data (0x0C)
/// </summary>
public sealed class ClosedStatusGumpData : ISubcommandData
{
    /// <summary>Character ID</summary>
    public uint CharacterId { get; set; }

    /// <inheritdoc />
    public int Length => 4;

    /// <inheritdoc />
    public void Read(SpanReader reader)
    {
        CharacterId = reader.ReadUInt32();
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(CharacterId);

        return writer.ToArray();
    }
}
