using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Movement;

/// <summary>
///     Records the steps it is asked about and told of, and allows them unless the test says otherwise.
/// </summary>
public sealed class RecordingFatigueService : IFatigueService
{
    public bool Allows { get; set; } = true;

    public List<bool> Asked { get; } = [];

    public List<bool> Taken { get; } = [];

    public bool CanStep(GameSession session, MobileEntity mobile, bool running)
    {
        Asked.Add(running);

        return Allows;
    }

    public List<(MobileEntity Mobile, bool Warn)> Loads { get; } = [];

    public void Stepped(GameSession session, MobileEntity mobile, bool running)
    {
        Taken.Add(running);
    }

    public void LoadChanged(GameSession session, MobileEntity mobile, bool warn)
    {
        Loads.Add((mobile, warn));
    }
}
