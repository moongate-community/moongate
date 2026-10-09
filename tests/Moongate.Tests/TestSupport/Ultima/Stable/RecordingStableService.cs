using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Stable;

namespace Moongate.Tests.TestSupport.Ultima.Stable;

/// <summary>
///     Keeps a list of stabled template ids per call and records what it is asked; it answers <see cref="Result" />.
/// </summary>
public sealed class RecordingStableService : IStableService
{
    public List<string> Pets { get; } = [];

    public StableResultType Result { get; set; } = StableResultType.Ok;

    public List<(MobileEntity Player, MobileEntity Pet)> Stables { get; } = [];

    public List<(MobileEntity Player, int Index, string Template)> Claims { get; } = [];

    public IReadOnlyList<string> Stabled(MobileEntity player)
    {
        return Pets;
    }

    public StableResultType TryStable(MobileEntity player, MobileEntity pet)
    {
        Stables.Add((player, pet));

        return Result;
    }

    public StableResultType TryClaim(MobileEntity player, int index, string template)
    {
        Claims.Add((player, index, template));

        return Result;
    }
}
