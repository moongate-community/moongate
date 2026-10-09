using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Combat;

/// <summary>
///     Records the mobiles that bled.
/// </summary>
public sealed class RecordingBloodService : IBloodService
{
    public List<MobileEntity> Splashed { get; } = [];

    public void Splash(MobileEntity target)
    {
        Splashed.Add(target);
    }
}
