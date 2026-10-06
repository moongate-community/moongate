using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Records what it is told, as "Aggressed 3 2", "Died 2", "Report 2 3", "Looted 3 4294967296" and "Restore 2" (serials).
/// </summary>
public sealed class RecordingMurderService : IMurderService
{
    public List<string> Calls { get; } = [];

    public void Aggressed(MobileEntity attacker, MobileEntity victim)
    {
        Calls.Add($"Aggressed {attacker.Id.Value} {victim.Id.Value}");
    }

    public void Died(MobileEntity victim)
    {
        Calls.Add($"Died {victim.Id.Value}");
    }

    public bool Report(MobileEntity victim, MobileEntity killer)
    {
        Calls.Add($"Report {victim.Id.Value} {killer.Id.Value}");

        return true;
    }

    public void Looted(MobileEntity looter, ItemEntity corpse)
    {
        Calls.Add($"Looted {looter.Id.Value} {corpse.Id.Value}");
    }

    public void Restore(MobileEntity mobile)
    {
        Calls.Add($"Restore {mobile.Id.Value}");
    }
}
