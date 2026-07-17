using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Hands out serials for virtual entities: things the client must be able to identify, but that are not
/// entities on the server. Hair and facial hair are the ones we have — they are properties of a mobile,
/// not items, yet every layer entry on the wire needs a serial.
/// </summary>
public interface IVirtualSerialService
{
    /// <summary>
    /// The serial for <paramref name="owner" />'s virtual entity on <paramref name="layer" />, allocating
    /// one the first time it is asked for. Stable afterwards, so the client keeps recognising the same
    /// object. Like ModernUO's, these are never persisted: they are reissued from scratch on each boot.
    /// </summary>
    Serial GetOrCreate(Serial owner, LayerType layer);
}
