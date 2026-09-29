using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Npcs;

/// <summary>
///     Records what it is told, as "Appeared 2" or "Moved 2 1496,1628,0".
/// </summary>
public sealed class RecordingNpcSenseService : INpcSenseService
{
    public List<string> Calls { get; } = [];

    public void Appeared(MobileEntity mobile)
    {
        Calls.Add($"Appeared {mobile.Id.Value}");
    }

    public void Moved(MobileEntity mobile, Point3D oldLocation)
    {
        Calls.Add($"Moved {mobile.Id.Value} {oldLocation.X},{oldLocation.Y},{oldLocation.Z}");
    }
}
