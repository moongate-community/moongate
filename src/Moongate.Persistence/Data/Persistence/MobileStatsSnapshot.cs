using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class MobileStatsSnapshot
{
    public int Strength { get; set; }

    public int Dexterity { get; set; }

    public int Intelligence { get; set; }
}
