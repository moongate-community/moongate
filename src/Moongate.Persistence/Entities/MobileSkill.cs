using Moongate.UO.Data.Types;

namespace Moongate.Persistence.Entities;

/// <summary>
/// One skill on a mobile, as ModernUO tracks it: its value, its personal ceiling, and whether it may
/// drift as it is used. <see cref="Value" /> and <see cref="Cap" /> are in tenths (500 = 50.0);
/// <see cref="Cap" /> defaults to the classic 100.0 and <see cref="Lock" /> to free-to-gain.
/// </summary>
public sealed record MobileSkill
{
    public int Value { get; set; }

    public int Cap { get; set; } = 1000;

    public SkillLockType Lock { get; set; }
}
