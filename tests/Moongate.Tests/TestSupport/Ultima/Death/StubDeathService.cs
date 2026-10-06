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

    /// <summary>
    ///     What <see cref="Kill" /> throws, when set.
    /// </summary>
    public Exception? KillFailure { get; set; }

    public List<(MobileEntity Mobile, MobileEntity? Killer)> Killed { get; } = [];

    public bool Kill(MobileEntity mobile, MobileEntity? killer = null)
    {
        if (KillFailure is not null)
        {
            throw KillFailure;
        }

        Killed.Add((mobile, killer));

        return Kills;
    }

    public List<MobileEntity> PlayersRaised { get; } = [];

    public bool Resurrect(MobileEntity player)
    {
        PlayersRaised.Add(player);

        return Kills;
    }

    /// <summary>
    ///     What every raising answers.
    /// </summary>
    public ResurrectResult Raises { get; set; } = new(ResurrectResultType.NotACorpse, null);

    /// <summary>
    ///     What every raising throws; null for none.
    /// </summary>
    public Exception? RaiseFailure { get; set; }

    public List<Serial> Raised { get; } = [];

    public Task<ResurrectResult> ResurrectAsync(Serial corpse, CancellationToken cancellationToken = default)
    {
        lock (Raised)
        {
            Raised.Add(corpse);
        }

        return RaiseFailure is null ? Task.FromResult(Raises) : Task.FromException<ResurrectResult>(RaiseFailure);
    }
}
