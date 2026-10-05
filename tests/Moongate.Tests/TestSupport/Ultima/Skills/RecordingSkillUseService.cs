using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Skills;

/// <summary>
///     Records the skills it is asked to use.
/// </summary>
public sealed class RecordingSkillUseService : ISkillUseService
{
    public List<(GameSession Session, SkillType Skill)> Used { get; } = [];

    public bool Use(GameSession session, SkillType skill)
    {
        Used.Add((session, skill));

        return true;
    }
}
