using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class MobileModifierCapsSnapshot
{
    public int PhysicalResist { get; set; }

    public int FireResist { get; set; }

    public int ColdResist { get; set; }

    public int PoisonResist { get; set; }

    public int EnergyResist { get; set; }

    public int DefenseChanceIncrease { get; set; }
}
