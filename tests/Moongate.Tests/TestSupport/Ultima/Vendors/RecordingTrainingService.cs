using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Vendors;

/// <summary>
///     Teaches the skills a test lists and records the quotes and the gold paid.
/// </summary>
public sealed class RecordingTrainingService : ITrainingService
{
    public List<SkillType> Skills { get; } = [];

    public bool Answer { get; set; } = true;

    public List<(MobileEntity Trainer, SkillType Skill)> Quoted { get; } = [];

    public List<(MobileEntity Trainer, ItemEntity Gold)> Paid { get; } = [];

    public IReadOnlyList<SkillType> Teachable(MobileEntity trainer, MobileEntity player)
    {
        return Skills;
    }

    public bool Quote(GameSession session, MobileEntity trainer, SkillType skill)
    {
        Quoted.Add((trainer, skill));

        return Answer;
    }

    public bool Pay(GameSession session, MobileEntity trainer, ItemEntity gold)
    {
        Paid.Add((trainer, gold));

        return Answer;
    }

    public void OnSessionClosed(GameSession session)
    {
    }
}
