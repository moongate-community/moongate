using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Movement;

/// <summary>
///     Records where each mobile stood when it was told of its step, running <see cref="OnCall" /> first so a test can
///     look at the state at that moment.
/// </summary>
public sealed class RecordingMoveOverService : IMoveOverService
{
    public List<Point3D> Steps { get; } = [];

    public Action? OnCall { get; set; }

    public void SteppedOn(MobileEntity mobile)
    {
        OnCall?.Invoke();
        Steps.Add(mobile.Location);
    }
}
