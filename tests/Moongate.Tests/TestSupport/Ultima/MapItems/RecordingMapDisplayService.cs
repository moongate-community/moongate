using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.TestSupport.Ultima.MapItems;

/// <summary>
///     Records the maps shown and the course changes handed to it; shows a map when <see cref="Shows" /> says so.
/// </summary>
public sealed class RecordingMapDisplayService : IMapDisplayService
{
    public bool Shows { get; set; } = true;

    public List<ItemEntity> Displayed { get; } = [];

    public List<MapCommandRequestPacket> Handled { get; } = [];

    public bool Display(GameSession session, ItemEntity map)
    {
        Displayed.Add(map);

        return Shows;
    }

    public void Handle(GameSession session, MapCommandRequestPacket packet)
    {
        Handled.Add(packet);
    }
}
