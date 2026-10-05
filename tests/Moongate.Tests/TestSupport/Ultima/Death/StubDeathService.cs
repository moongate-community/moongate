using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Death;

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

    /// <summary>
    ///     What every raising answers.
    /// </summary>
    public ResurrectResult Raises { get; set; } = new(ResurrectResultType.NotACorpse, null);

    public List<Serial> Raised { get; } = [];

    public Task<ResurrectResult> ResurrectAsync(Serial corpse, CancellationToken cancellationToken = default)
    {
        lock (Raised)
        {
            Raised.Add(corpse);
        }

        return Task.FromResult(Raises);
    }
}
