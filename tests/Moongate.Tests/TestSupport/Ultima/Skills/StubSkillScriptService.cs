using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Skills;

/// <summary>
///     Records the skills whose script it is asked to call and answers <see cref="Result" />.
/// </summary>
public sealed class StubSkillScriptService : ISkillScriptService
{
    public ScriptResult Result { get; set; } = ScriptResult.Completed([]);

    /// <summary>
    ///     What the script does while it runs.
    /// </summary>
    public Action? During { get; set; }

    public List<(SkillType Skill, MobileEntity User)> Used { get; } = [];

    public ScriptResult Use(SkillType skill, MobileEntity user)
    {
        Used.Add((skill, user));
        During?.Invoke();

        return Result;
    }
}
