using Moongate.Ultima.Data;

namespace Moongate.Ultima.Interfaces;

/// <summary>
/// High-level item facade over tiledata and art. Requires the client directory to be
/// set via <c>Files.SetDirectory</c> before use.
/// </summary>
public interface IItemCatalog
{
    /// <summary>Full item information, or null when the id is outside the item table.</summary>
    UoItemInfo? GetItem(uint itemId);

    /// <summary>PNG-encoded item art (optionally hued), or null when the art is missing.</summary>
    Stream? GetItemImage(uint itemId, ushort hue = 0);
}
