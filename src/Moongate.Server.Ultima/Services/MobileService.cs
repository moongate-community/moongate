using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the mobiles in the world and hands out virtual serials for hair and beard the first time they are needed,
///     counting up from <see cref="Serial.MinVirtual" /> as ModernUO does.
/// </summary>
public sealed class MobileService : IMobileService
{
    private readonly ConcurrentDictionary<Serial, Serial> _hair = new();
    private readonly ConcurrentDictionary<Serial, Serial> _beard = new();
    private readonly ConcurrentDictionary<Serial, byte> _inWorld = new();
    private long _nextVirtual = Serial.MinVirtual;

    public IReadOnlyCollection<Serial> InWorld => _inWorld.Keys.ToArray();

    public Serial HairSerial(Serial mobile)
    {
        return _hair.GetOrAdd(mobile, _ => NextVirtual());
    }

    public Serial BeardSerial(Serial mobile)
    {
        return _beard.GetOrAdd(mobile, _ => NextVirtual());
    }

    public void EnterWorld(Serial mobile)
    {
        _inWorld[mobile] = 0;
    }

    public bool IsInWorld(Serial mobile)
    {
        return _inWorld.ContainsKey(mobile);
    }

    private Serial NextVirtual()
    {
        // The range holds about 17 million serials; wrap around rather than leave it.
        const long rangeSize = (long)Serial.MaxVirtual - Serial.MinVirtual + 1;
        var offset = (Interlocked.Increment(ref _nextVirtual) - 1 - Serial.MinVirtual) % rangeSize;

        return new Serial((uint)(Serial.MinVirtual + offset));
    }
}
