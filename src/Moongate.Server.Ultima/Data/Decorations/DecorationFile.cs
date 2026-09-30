using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Decorations;

/// <summary>
///     One file under <c>templates/decorations/&lt;folder&gt;/</c> and the maps its folder decorates.
/// </summary>
public sealed class DecorationFile
{
    /// <summary>
    ///     The folder, such as <c>britannia</c>.
    /// </summary>
    public required string Folder { get; init; }

    /// <summary>
    ///     The file name without its extension, such as <c>britain</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The maps the blocks are placed on: Trammel and Felucca for <c>britannia</c>, otherwise the folder's map.
    /// </summary>
    public required IReadOnlyList<MapType> Maps { get; init; }

    public IReadOnlyList<DecorationBlock> Blocks { get; init; } = [];
}
