using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Death;

/// <summary>
///     Records who was killed and by whom, and answers <see cref="Kills" />.
/// </summary>
public sealed class StubDeathService : IDeathService
{
    public bool Kills { get; set; } = true;

    public List<(MobileEntity Mobile, MobileEntity? Killer)> Killed { get; } = [];

    public bool Kill(MobileEntity mobile, MobileEntity? killer = null)
    {
        Killed.Add((mobile, killer));

        return Kills;
    }
}
