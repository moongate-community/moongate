using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Modules.Internal;

/// <summary>
///     Who was attended to a moment ago. Several NPCs hear the same words in the same moment: each asks, the first is told
///     yes, and the others are told no for half a second.
/// </summary>
internal sealed class Attendance
{
    private const int Limit = 256;

    private static readonly TimeSpan For = TimeSpan.FromMilliseconds(500);

    private readonly Dictionary<Serial, DateTimeOffset> _attended = new();

    /// <summary>
    ///     Gets whether the caller attends to <paramref name="who" /> now: true for the first that asks, false for whoever asks
    ///     again within half a second.
    /// </summary>
    public bool TryAttend(Serial who, DateTimeOffset now)
    {
        if (_attended.TryGetValue(who, out var last) && now - last < For && now >= last)
        {
            return false;
        }

        // The players who left are forgotten as the list is used.
        if (_attended.Count > Limit)
        {
            foreach (var gone in _attended.Where(entry => now - entry.Value >= For).Select(entry => entry.Key).ToArray())
            {
                _attended.Remove(gone);
            }
        }

        _attended[who] = now;

        return true;
    }
}
