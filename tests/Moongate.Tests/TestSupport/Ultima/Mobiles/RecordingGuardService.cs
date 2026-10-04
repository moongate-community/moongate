using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Records what the guards were told the players said, and sends nobody.
/// </summary>
public sealed class RecordingGuardService : IGuardService
{
    public List<(MobileEntity Speaker, string Text, IReadOnlyList<int> Keywords)> Heard { get; } = [];

    void IGuardService.Heard(MobileEntity speaker, string text, IReadOnlyList<int>? keywords)
    {
        Heard.Add((speaker, text, keywords ?? []));
    }

    public int Call(MobileEntity caller)
    {
        return 0;
    }
}
