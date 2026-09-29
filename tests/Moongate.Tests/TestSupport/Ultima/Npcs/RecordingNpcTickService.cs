using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Npcs;

/// <summary>
///     Tracks which NPCs are awake and counts every wake and sleep that changed something.
/// </summary>
public sealed class RecordingNpcTickService : INpcTickService
{
    private readonly HashSet<Serial> _awake = [];

    public int Wakes { get; private set; }

    public int Sleeps { get; private set; }

    public int AwakeCount => _awake.Count;

    public long ThinkCount => 0;

    public bool IsAwake(Serial serial)
    {
        return _awake.Contains(serial);
    }

    public void Wake(MobileEntity npc)
    {
        if (npc.IsNpc && _awake.Add(npc.Id))
        {
            Wakes++;
        }
    }

    public void Sleep(MobileEntity npc)
    {
        if (_awake.Remove(npc.Id))
        {
            Sleeps++;
        }
    }
}
