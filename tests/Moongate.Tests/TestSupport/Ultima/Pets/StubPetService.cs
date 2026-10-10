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

    public bool Releases { get; set; } = true;

    public List<(MobileEntity Player, MobileEntity Creature)> Released { get; } = [];

    public bool Release(MobileEntity player, MobileEntity creature)
    {
        Released.Add((player, creature));

        return Releases;
    }

    public int LoyaltyOf { get; set; } = 100;

    public double Chance { get; set; } = 1;

    public PetObeyResultType ObeyResult { get; set; } = PetObeyResultType.Obeyed;

    public PetFeedResultType FeedResult { get; set; } = PetFeedResultType.Fed;

    public List<(MobileEntity Player, MobileEntity Creature)> Obeys { get; } = [];

    public List<(string? Template, int Amount)> Feeds { get; } = [];

    public int Loyalty(MobileEntity creature)
    {
        return LoyaltyOf;
    }

    public int AdjustLoyalty(MobileEntity creature, int delta)
    {
        LoyaltyOf = Math.Clamp(LoyaltyOf + delta, 0, 100);

        return LoyaltyOf;
    }

    public List<MobileEntity> LetGoOf { get; } = [];

    public bool LetGo(MobileEntity creature)
    {
        LetGoOf.Add(creature);

        return true;
    }

    public double ControlChance(MobileEntity player, MobileEntity creature)
    {
        return Chance;
    }

    public PetObeyResultType Obey(MobileEntity player, MobileEntity creature)
    {
        Obeys.Add((player, creature));

        return ObeyResult;
    }

    public bool Bonded { get; set; }

    public bool IsBonded(MobileEntity creature)
    {
        return Bonded;
    }

    public Action? OnFeed { get; set; }

    public PetFeedResultType Feed(MobileEntity player, MobileEntity creature, string? itemTemplate, int amount)
    {
        Feeds.Add((itemTemplate, amount));
        OnFeed?.Invoke();

        return FeedResult;
    }

    public Dictionary<string, int> Slots { get; } = [];

    public int SlotsOf(string? templateId)
    {
        return templateId is not null && Slots.TryGetValue(templateId, out var slots) ? slots : 1;
    }

    public PetResultType TryTame(MobileEntity player, MobileEntity creature)
    {
        Tames.Add((player, creature));

        return Result;
    }
}
