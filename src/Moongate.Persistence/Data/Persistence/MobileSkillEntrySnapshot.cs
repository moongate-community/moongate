using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class MobileSkillEntrySnapshot
{
    public int SkillId { get; set; }

    public double Value { get; set; }

    public double Base { get; set; }

    public int Cap { get; set; }

    public byte Lock { get; set; }
}
