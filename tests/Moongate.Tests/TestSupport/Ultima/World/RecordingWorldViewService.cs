using Moongate.Core.Geometry;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Records the calls it gets, in order, as "Entered 2 10", "Moved 2 1496,1628,10 run", "Left 2", "Appeared 7",
///     "ShownTo 2 7" and "Disappeared 7", running <see cref="OnCall" /> first so a test can look at the state at that
///     moment.
/// </summary>
public sealed class RecordingWorldViewService : IWorldViewService
{
    public List<string> Calls { get; } = [];

    public Action<string>? OnCall { get; set; }

    public void Entered(MobileEntity mobile, long sessionId, ClientVersion? version)
    {
        Record($"Entered {mobile.Id.Value} {sessionId}");
    }

    public void Moved(MobileEntity mobile, Point3D oldLocation, bool running)
    {
        Record($"Moved {mobile.Id.Value} {oldLocation.X},{oldLocation.Y},{oldLocation.Z}{(running ? " run" : "")}");
    }

    public void Left(MobileEntity mobile)
    {
        Record($"Left {mobile.Id.Value}");
    }

    public void ItemAppeared(ItemEntity item)
    {
        Record($"Appeared {item.Id.Value}");
    }

    public void ShowItemTo(MobileEntity viewer, ItemEntity item)
    {
        Record($"ShownTo {viewer.Id.Value} {item.Id.Value}");
    }

    public void ItemDisappeared(ItemEntity item)
    {
        Record($"Disappeared {item.Id.Value}");
    }

    private void Record(string call)
    {
        OnCall?.Invoke(call);
        Calls.Add(call);
    }
}
