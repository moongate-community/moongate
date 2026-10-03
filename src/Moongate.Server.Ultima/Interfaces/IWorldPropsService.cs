using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The values the shard as a whole keeps across restarts, read at startup and written by the world save: what
///     scripts set with <c>world.set_prop</c>.
/// </summary>
/// <remarks>
///     Game loop only, once started.
/// </remarks>
public interface IWorldPropsService : IMoongateStartupService
{
    /// <summary>
    ///     Gets the live state, which the world save writes.
    /// </summary>
    WorldStateEntity State { get; }

    /// <summary>
    ///     Gets the value kept under <paramref name="key" />; null when there is none.
    /// </summary>
    object? Get(string key);

    /// <summary>
    ///     Keeps <paramref name="value" /> under <paramref name="key" />, a string, a number or a bool; null removes it.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The key is empty, or the value is not a string, a number, a bool or an enum.
    /// </exception>
    void Set(string key, object? value);
}
