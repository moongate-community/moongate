using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Combat;

/// <summary>
///     Records who was told to attack whom and who was stopped, and answers <see cref="Allows" /> to an attack.
/// </summary>
public sealed class RecordingCombatService : ICombatService
{
    public bool Allows { get; set; } = true;

    public List<(MobileEntity Attacker, MobileEntity Target)> Attacks { get; } = [];

    public List<MobileEntity> Stopped { get; } = [];

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public bool Attack(MobileEntity attacker, MobileEntity target)
    {
        Attacks.Add((attacker, target));

        return Allows;
    }

    public void Stop(MobileEntity mobile)
    {
        Stopped.Add(mobile);
    }

    /// <summary>
    ///     What <see cref="RangeOf" /> answers: the melee range, 1, unless a test sets it.
    /// </summary>
    public int Range { get; set; } = 1;

    public int RangeOf(MobileEntity mobile)
    {
        return Range;
    }

    public MobileEntity? TargetOf(MobileEntity mobile)
    {
        return Attacks.LastOrDefault(attack => attack.Attacker == mobile).Target;
    }
}
