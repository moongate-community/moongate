using Moongate.Server.Ultima.Data.Decorations;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Places the world decoration of <c>templates/decorations</c> as fixed items that never decay.
/// </summary>
public interface IDecorationService
{
    /// <summary>
    ///     Places every file, one game-loop work item each, reporting each file when it is done. Doors get the
    ///     <c>decoration_door</c> template and adjacent doors of the same kind are linked; kinds with their own logic
    ///     (teleporters, spawners, mark containers, moongates, addons) are skipped; an item with the same graphic already
    ///     on the spot is kept, so running it again places only what is missing.
    /// </summary>
    Task<DecorationResult> DecorateAsync(
        IProgress<DecorationFileResult>? progress = null,
        CancellationToken cancellationToken = default
    );
}
