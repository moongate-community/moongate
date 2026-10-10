using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Records the mobiles whose timed effects were ended, taking their bonuses away as the service does.
/// </summary>
public sealed class RecordingStatBonusService : IStatBonusService
{
    public List<MobileEntity> Ended { get; } = [];

    public bool TryAddBonus(MobileEntity mobile, StatBonusType stat, int amount, TimeSpan duration)
    {
        return false;
    }

    public bool TryAddCurse(MobileEntity mobile, StatBonusType stat, int amount, TimeSpan duration)
    {
        return false;
    }

    public int Bonus(MobileEntity mobile, StatBonusType stat)
    {
        return 0;
    }

    public bool TrySetNightSight(MobileEntity mobile, int level, TimeSpan duration)
    {
        return false;
    }

    public bool HasNightSight(MobileEntity mobile)
    {
        return false;
    }

    public void EndAll(MobileEntity mobile)
    {
        Ended.Add(mobile);
        mobile.StrengthBonus = 0;
        mobile.DexterityBonus = 0;
        mobile.IntelligenceBonus = 0;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
    }

    public void Left(Serial player)
    {
    }
}
