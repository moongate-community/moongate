using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Records who was made a criminal and who was pardoned, and keeps the flag on the mobile.
/// </summary>
public sealed class RecordingCrimeService : ICrimeService
{
    public List<string> Calls { get; } = [];

    public bool IsCriminal(MobileEntity mobile)
    {
        return mobile.Criminal;
    }

    public void MakeCriminal(MobileEntity mobile)
    {
        mobile.Criminal = true;
        Calls.Add($"criminal {mobile.Id.Value}");
    }

    public void Pardon(MobileEntity mobile)
    {
        mobile.Criminal = false;
        Calls.Add($"pardon {mobile.Id.Value}");
    }
}
