namespace Moongate.Http.Plugin.Interfaces;

/// <summary>
/// Serialises every read of Moongate.Ultima's process-wide statics.
/// <para>
/// One gate for the whole library, not one per caller. Art holds no lock of its own and shares an LRU
/// cache, a file-index stream position and a static scratch buffer across calls; Map locks its own block
/// cache but descends into Art regardless, and RadarCol and TileData are equally unguarded. Two callers
/// holding separate gates corrupt that state exactly as two holding none.
/// </para>
/// </summary>
public interface IUltimaReadGate
{
    /// <summary>
    /// Runs a read against the client files, serialised against every other reader. The delegate holds the
    /// gate for its whole duration, so it must be a read of the client files and nothing else: no other
    /// I/O, no waiting on anything, no calls back into a service that takes this gate again.
    /// </summary>
    Task<T> ReadAsync<T>(Func<T> read, CancellationToken cancellationToken = default);
}
