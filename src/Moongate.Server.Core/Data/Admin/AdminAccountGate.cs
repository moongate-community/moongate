
namespace Moongate.Server.Core.Data.Admin;

/// <summary>Immutable administration accountgate snapshot.</summary>
public sealed class AdminAccountGate
{
    public Guid Generation { get; }
    public bool Blocked { get; }

    public AdminAccountGate(Guid generation, bool blocked)
    {
        Generation = generation;
        Blocked = blocked;
    }
}
