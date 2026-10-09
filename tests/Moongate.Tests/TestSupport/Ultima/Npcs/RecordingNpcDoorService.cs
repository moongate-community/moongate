using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Npcs;

/// <summary>
///     Says whether NPCs open doors and whether one stood ahead, and records which doors were asked for.
/// </summary>
public sealed class RecordingNpcDoorService : INpcDoorService
{
    public bool Opens { get; set; }

    public bool DoorAhead { get; set; }

    public List<(MobileEntity Npc, DirectionType Direction)> Tried { get; } = [];

    public bool OpensDoors(MobileEntity npc)
    {
        return Opens;
    }

    public bool TryOpen(MobileEntity npc, DirectionType direction)
    {
        Tried.Add((npc, direction));

        return Opens && DoorAhead;
    }
}
