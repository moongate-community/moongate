using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;

namespace Moongate.UO.Data.Packets.GeneralInformation.SubCommands;

/// <summary>
/// Cast Targeted Spell subcommand data (0x2D)
/// </summary>
public sealed class CastTargetedSpellData : ISubcommandData
{
    /// <summary>Spell ID</summary>
    public ushort SpellId { get; set; }

    /// <summary>Target serial</summary>
    public uint TargetSerial { get; set; }

    /// <inheritdoc />
    public int Length => 6;

    /// <inheritdoc />
    public void Read(SpanReader reader)
    {
        SpellId = reader.ReadUInt16();
        TargetSerial = reader.ReadUInt32();
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(SpellId);
        writer.Write(TargetSerial);

        return writer.ToArray();
    }
}
