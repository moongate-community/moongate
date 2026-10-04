using Moongate.Core.Primitives;
using Moongate.Core.Geometry;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Records the calls it gets, in order, as "Entered 2 10", "Moved 2 1496,1628,10 run", "Teleported 2 Trammel 1496,1628,10", "Left 2",
///     "MobileAppeared 9", "Appeared 7", "ShownTo 2 7" and "Disappeared 7", running <see cref="OnCall" /> first so a
///     test can look at the state at that moment.
/// </summary>
public sealed class RecordingWorldViewService : IWorldViewService
{
    public List<string> Calls { get; } = [];

    public Action<string>? OnCall { get; set; }

    public void Entered(MobileEntity mobile, long sessionId, ClientVersion? version, AccountType account = AccountType.Regular)
    {
        Record($"Entered {mobile.Id.Value} {sessionId}");
    }

    public void Teleported(MobileEntity mobile, MapType oldMap, Point3D oldLocation)
    {
        Record($"Teleported {mobile.Id.Value} {oldMap} {oldLocation.X},{oldLocation.Y},{oldLocation.Z}");
    }

    public void Moved(MobileEntity mobile, Point3D oldLocation, bool running)
    {
        Record($"Moved {mobile.Id.Value} {oldLocation.X},{oldLocation.Y},{oldLocation.Z}{(running ? " run" : "")}");
    }

    public void Left(MobileEntity mobile)
    {
        Record($"Left {mobile.Id.Value}");
    }

    public void MobileAppeared(MobileEntity mobile)
    {
        Record($"MobileAppeared {mobile.Id.Value}");
    }

    public void MobileAnimated(MobileEntity mobile, int action, int frameCount, int repeatCount)
    {
        Record($"Animated {mobile.Id.Value} {action} {frameCount} {repeatCount}");
    }

    public void MobileFlagsChanged(MobileEntity mobile)
    {
        Record($"FlagsChanged {mobile.Id.Value}");
    }

    public void MobileHiddenChanged(MobileEntity mobile)
    {
        Record($"HiddenChanged {mobile.Id.Value}");
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

    public void ContainedItemAppeared(ItemEntity item, ItemEntity root, Serial except)
    {
        Record($"ContainedAppeared {item.Id.Value} in {root.Id.Value} except {except.Value}");
    }

    public void ContainedItemDisappeared(ItemEntity item, ItemEntity root, Serial except)
    {
        Record($"ContainedDisappeared {item.Id.Value} in {root.Id.Value} except {except.Value}");
    }

    public void WornItemChanged(MobileEntity wearer, ItemEntity item)
    {
        Record($"Worn {wearer.Id.Value} {item.Id.Value}");
    }

    public void WornItemRemoved(MobileEntity wearer, ItemEntity item)
    {
        Record($"Unworn {wearer.Id.Value} {item.Id.Value}");
    }

    private void Record(string call)
    {
        OnCall?.Invoke(call);
        Calls.Add(call);
    }
}
