using Moongate.Core.Primitives;
using Moongate.Server.Interfaces.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// Allocates virtual serials from the band <see cref="Serial.MinVirtual" />..<see cref="Serial.MaxVirtual" />,
/// which no real item can occupy, and remembers which one each owner's layer got so it stays put.
/// <para>
/// The counter wraps rather than running out, as ModernUO's does: a virtual serial only has to be unique
/// among the things a client can currently see, and by the time ~17.9 million have been handed out the
/// early ones are long forgotten.
/// </para>
/// </summary>
public sealed class VirtualSerialService : IVirtualSerialService
{
    private readonly Dictionary<(Serial Owner, LayerType Layer), Serial> _serials = [];
    private readonly Lock _sync = new();

    private uint _next = Serial.MinVirtual;

    public Serial GetOrCreate(Serial owner, LayerType layer)
    {
        lock (_sync)
        {
            if (_serials.TryGetValue((owner, layer), out var existing))
            {
                return existing;
            }

            var serial = new Serial(_next);
            _next = _next >= Serial.MaxVirtual ? Serial.MinVirtual : _next + 1;
            _serials[(owner, layer)] = serial;

            return serial;
        }
    }
}
