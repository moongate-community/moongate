using MemoryPack;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Skills;

/// <summary>
/// Represents SkillEntry.
/// </summary>
[MemoryPackable(SerializeLayout.Explicit)]
public partial class SkillEntry
{
    [MemoryPackOrder(0)]
    public double Value { get; set; }

    [MemoryPackIgnore]
    public SkillInfo Skill { get; set; } = null!;

    [MemoryPackOrder(1)]
    public double Base { get; set; } = 0;

    [MemoryPackOrder(2)]
    public int Cap { get; set; }

    [MemoryPackOrder(3)]
    public UOSkillLock Lock { get; set; }
}
