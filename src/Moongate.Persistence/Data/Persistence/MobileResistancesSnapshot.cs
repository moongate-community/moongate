using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class MobileResistancesSnapshot
{
    public int Physical { get; set; }

    public int Fire { get; set; }

    public int Cold { get; set; }

    public int Poison { get; set; }

    public int Energy { get; set; }
}
