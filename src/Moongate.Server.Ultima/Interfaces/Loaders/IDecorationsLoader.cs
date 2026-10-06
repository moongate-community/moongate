using Moongate.Server.Ultima.Data.Decorations;

namespace Moongate.Server.Ultima.Interfaces.Loaders;

/// <summary>
///     Reads the decoration files under <c>templates/decorations/</c> when the world is decorated.
/// </summary>
public interface IDecorationsLoader
{
    /// <summary>
    ///     Reads every <c>&lt;folder&gt;/*.toml</c> in folder and file order, skipping the folders whose name starts
    ///     with <c>_</c>; no directory gives no files.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     A folder is not a map or <c>britannia</c>, or a block is malformed.
    /// </exception>
    Task<IReadOnlyList<DecorationFile>> LoadAsync(CancellationToken cancellationToken = default);
}
