using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Pets;

namespace Moongate.Tests.TestSupport.Ultima.Pets;

/// <summary>
///     Answers the followers and the limit it is given, and the result it is told to for a tame; records the tames.
/// </summary>
public sealed class StubPetService : IPetService
{
    public int MaxFollowers { get; set; } = 5;

    public int FollowerCount { get; set; }

    public PetResultType Result { get; set; } = PetResultType.Ok;

    public List<(MobileEntity Player, MobileEntity Creature)> Tames { get; } = [];

    public int Followers(MobileEntity player)
    {
        return FollowerCount;
    }

    public List<Serial> ChangedFor { get; } = [];

    public void Changed(Serial player)
    {
        ChangedFor.Add(player);
    }

    public int SlotsOf(string? templateId)
    {
        return 1;
    }

    public PetResultType TryTame(MobileEntity player, MobileEntity creature)
    {
        Tames.Add((player, creature));

        return Result;
    }
}
