using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class MobileResourcesSnapshot
{
    public int Hits { get; set; }

    public int MaxHits { get; set; }

    public int Mana { get; set; }

    public int MaxMana { get; set; }

    public int Stamina { get; set; }

    public int MaxStamina { get; set; }
}
