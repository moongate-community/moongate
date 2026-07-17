using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.World;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Builds and caches object property lists (tooltips). Loop-affine like every game service:
/// the cache is unsynchronized on purpose.
/// </summary>
public interface IOplService
{
    /// <summary>Returns the cached property list for the serial, building it on first access. Unknown serials yield an empty snapshot.</summary>
    OplSnapshot GetOrBuild(Serial serial);

    /// <summary>Drops the cached list so the next request rebuilds it.</summary>
    void Invalidate(Serial serial);
}
