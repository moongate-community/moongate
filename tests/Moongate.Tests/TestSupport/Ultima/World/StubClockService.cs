using Moongate.Server.Ultima.Data.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.World;

/// <summary>
///     Gives the same time of day everywhere, which a test sets.
/// </summary>
public sealed class StubClockService : IClockService
{
    public GameTime Time { get; set; } = new(12, 0);

    public GameTime GetTime(MapType map, int x)
    {
        return Time;
    }
}
