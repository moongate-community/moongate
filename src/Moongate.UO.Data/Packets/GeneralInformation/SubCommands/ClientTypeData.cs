using Moongate.Core.Spans;
using Moongate.UO.Data.Extensions;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;
using Moongate.Uo.Data.Types;

namespace Moongate.Uo.Services.Network.Packets.GeneralInformation.SubCommands;

/// <summary>
/// Client Type subcommand data (0x0F)
/// </summary>
public sealed class ClientTypeData : ISubcommandData
{
    /// <summary>Unknown field, always 0x0A</summary>
    public byte Unknown { get; set; } = 0x0A;

    /// <summary>Client type flags</summary>
    public uint ClientFlags { get; set; }

    /// <summary>Gets parsed client flags</summary>
    public SubcommandClientFlag ParsedFlags => SubCommandSubcommandClientFlagExtensions.Parse((int)ClientFlags);

    /// <inheritdoc />
    public int Length => 5;

    /// <inheritdoc />
    public void Read(SpanReader reader)
    {
        Unknown = reader.ReadByte();
        ClientFlags = reader.ReadUInt32();
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(Unknown);
        writer.Write(ClientFlags);

        return writer.ToArray();
    }
}
