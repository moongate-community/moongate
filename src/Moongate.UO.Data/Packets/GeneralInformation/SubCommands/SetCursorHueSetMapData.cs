using Moongate.Core.Spans;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;

namespace Moongate.UO.Data.Packets.GeneralInformation.SubCommands;

/// <summary>
/// Set Cursor Hue/Set Map subcommand data (0x08)
/// </summary>
public sealed class SetCursorHueSetMapData : ISubcommandData
{
    /// <summary>
    /// Map ID: 0=Felucca, 1=Trammel, 2=Ilshenar, 3=Malas, 4=Tokuno, 5=TerMur
    /// </summary>
    public byte MapId { get; set; }

    /// <inheritdoc />
    public int Length => 1;

    public SetCursorHueSetMapData()
    {

    }
    public SetCursorHueSetMapData(Map? map = null)
    {
        if (map != null)
        {
            MapId = (byte)map.MapID;
        }

    }

    /// <inheritdoc />
    public void Read(SpanReader reader)
    {
        MapId = reader.ReadByte();
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(MapId);
        return writer.ToArray();
    }
}
