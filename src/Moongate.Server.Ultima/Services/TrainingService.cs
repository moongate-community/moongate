using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Training;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Training;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Training;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The skill trainers, as ModernUO's <c>Teach</c> and <c>CheckTeachSkills</c>. Skills are in tenths of a point.
/// </summary>
public sealed class TrainingService : ITrainingService
{
    private const int Tenths = 10;
    private const int TeachingFloor = 600;
    private const int TeachingDivisor = 3;
    private const int TeachingCeiling = 420;
    private const int ClilocPrice = 1019077;
    private const int ClilocForLessLess = 1043108;
    private const int ClilocKnowsMore = 501508;
    private const int ClilocKnowsAll = 501509;
    private const int ClilocNotRaisable = 501510;
    private const int ClilocShowing = 501539;
    private const int ClilocIncreased = 501540;
    private const int WarningHue = 0x22;

    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly IItemHandlingService _handling;
    private readonly ISpeechService _speech;
    private readonly SkillsConfig _skills;
    private readonly ItemsConfig _items;

    public TrainingService(
        IMobileService mobiles,
        IMobileStateService state,
        IItemHandlingService handling,
        ISpeechService speech,
        SkillsConfig skills,
        ItemsConfig items
    )
    {
        _mobiles = mobiles;
        _state = state;
        _handling = handling;
        _speech = speech;
        _skills = skills;
        _items = items;
    }

    public IReadOnlyList<SkillType> Teachable(MobileEntity trainer, MobileEntity player)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        ArgumentNullException.ThrowIfNull(player);

        if (player.IsNpc || player.IsDead)
        {
            return [];
        }

        return trainer.Skills.Where(known => known.Base >= TeachingFloor)
            .Where(known => Taught(known) > (Known(player, known.Skill)?.Base ?? 0))
            .Select(known => known.Skill)
            .ToList();
    }

    public bool Quote(GameSession session, MobileEntity trainer, SkillType skill)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(trainer);

        if (!TryPlayer(session, out var player))
        {
            return false;
        }

        var offer = Offer(trainer, player, skill);

        switch (offer.Result)
        {
            case TrainingResultType.Ok:
                _speech.SayCliloc(trainer, ClilocPrice, "", $" {offer.Points}");
                _speech.SayCliloc(trainer, ClilocForLessLess);
                session.Set(TrainingSessionKeys.Quote, new TrainingQuote(trainer.Id, skill));

                return true;
            case TrainingResultType.KnowsMore:
                _speech.SayCliloc(trainer, ClilocKnowsMore);

                break;
            case TrainingResultType.KnowsAll:
                _speech.SayCliloc(trainer, ClilocKnowsAll);

                break;
            case TrainingResultType.NotRaisable:
                _speech.TellCliloc(player, ClilocNotRaisable, "", WarningHue);

                break;
        }

        return false;
    }

    public bool Pay(GameSession session, MobileEntity trainer, ItemEntity gold)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(trainer);
        ArgumentNullException.ThrowIfNull(gold);

        if (gold.TemplateId != _items.GoldTemplate ||
            session.Get(TrainingSessionKeys.Quote) is not { } quote ||
            quote.Trainer != trainer.Id ||
            !TryPlayer(session, out var player))
        {
            return false;
        }

        // The state may have changed since the quote: the offer is worked out again.
        var offer = Offer(trainer, player, quote.Skill);

        if (offer.Result != TrainingResultType.Ok)
        {
            session.Set(TrainingSessionKeys.Quote, null);

            return false;
        }

        var points = Math.Min(offer.Points, gold.Amount);

        if (!_handling.Consume(gold, points))
        {
            return false;
        }

        MakeRoom(player, quote.Skill, points);
        _state.SetSkill(player, quote.Skill, (Known(player, quote.Skill)?.Base ?? 0) + points);
        session.Set(TrainingSessionKeys.Quote, null);
        _speech.SayCliloc(trainer, ClilocShowing);
        _speech.TellCliloc(player, ClilocIncreased);

        return true;
    }

    public void OnSessionClosed(GameSession session)
    {
        if (session.Get(TrainingSessionKeys.Quote) is not null)
        {
            session.Set(TrainingSessionKeys.Quote, null);
        }
    }

    // What a trainer teaches of a skill it has: a third of it, 42.0 at most.
    private static int Taught(MobileSkill known)
    {
        return Math.Min(known.Base / TeachingDivisor, TeachingCeiling);
    }

    private static MobileSkill? Known(MobileEntity mobile, SkillType skill)
    {
        return mobile.Skills.FirstOrDefault(known => known.Skill == skill);
    }

    private bool TryPlayer(GameSession session, [NotNullWhen(true)] out MobileEntity? player)
    {
        return _mobiles.TryGet(session.CharacterId, out player) && !player.IsNpc;
    }

    private (TrainingResultType Result, int Points) Offer(MobileEntity trainer, MobileEntity player, SkillType skill)
    {
        var teaching = Known(trainer, skill);
        // A player's skill list may not hold a skill it never used: it starts at zero, with the default cap.
        var learning = Known(player, skill) ?? new MobileSkill { Skill = skill };

        if (player.IsNpc || player.IsDead || teaching is null || teaching.Base < TeachingFloor)
        {
            return (TrainingResultType.NotTeaching, 0);
        }

        var baseToSet = Math.Min(Taught(teaching), learning.Cap);
        var points = baseToSet - learning.Base;

        if (points < 0)
        {
            return (TrainingResultType.KnowsMore, 0);
        }

        if (points == 0)
        {
            return (TrainingResultType.KnowsAll, 0);
        }

        if (learning.Lock != SkillLockType.Up)
        {
            return (TrainingResultType.NotRaisable, 0);
        }

        var room = Room(player, skill);

        return room == 0 ? (TrainingResultType.NotRaisable, 0) : (TrainingResultType.Ok, Math.Min(points, room));
    }

    // The points that fit under the total cap: what is free, and what the skills locked down could give.
    private int Room(MobileEntity player, SkillType skill)
    {
        return Free(player) + Freeable(player, skill);
    }

    private int Free(MobileEntity player)
    {
        return Math.Max(_skills.TotalCap * Tenths - player.Skills.Sum(known => known.Base), 0);
    }

    private static int Freeable(MobileEntity player, SkillType skill)
    {
        return player.Skills.Where(known => known.Skill != skill && known.Lock == SkillLockType.Down)
            .Sum(known => known.Base);
    }

    // Skills locked down give way, in order, to the points the total cap does not have free.
    private void MakeRoom(MobileEntity player, SkillType skill, int points)
    {
        var needed = points - Free(player);

        foreach (var other in player.Skills.Where(known => known.Skill != skill && known.Lock == SkillLockType.Down).ToList())
        {
            if (needed <= 0)
            {
                break;
            }

            var taken = Math.Min(other.Base, needed);
            _state.SetSkill(player, other.Skill, other.Base - taken);
            needed -= taken;
        }
    }
}
