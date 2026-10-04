using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Weight;

/// <summary>
///     Weights set by the test: what every mobile carries and may carry, and whether containers hold.
/// </summary>
public sealed class StubWeightService : IWeightService
{
    public int CarriedStones { get; set; }

    public int MaximumStones { get; set; } = 100;

    public bool HoldsResult { get; set; } = true;

    public int Of(ItemEntity item)
    {
        return 1;
    }

    public int Carried(MobileEntity mobile)
    {
        return CarriedStones;
    }

    public int MaxCarried(MobileEntity mobile)
    {
        return MaximumStones;
    }

    public bool Holds(ItemEntity container, ItemEntity item)
    {
        return HoldsResult;
    }
}
