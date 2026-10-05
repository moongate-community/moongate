using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces.Books;

/// <summary>
///     Creates, inscribes and displays readable item snapshots on the game loop.
/// </summary>
public interface IBookDocumentService
{
    /// <summary>
    ///     Gives a resolved document to the recipient; failure creates no item.
    /// </summary>
    ItemEntity? Give(MobileEntity recipient, string templateId, IReadOnlyDictionary<string, object?>? values = null, string? recordedPlayerName = null);

    /// <summary>
    ///     Writes all resolved fields to a supported item; failure leaves it unchanged.
    /// </summary>
    bool Write(ItemEntity item, MobileEntity recipient, string templateId, IReadOnlyDictionary<string, object?>? values = null, string? recordedPlayerName = null);

    /// <summary>
    ///     Opens saved text for an eligible reader. From Lua, true means queued; access is checked again before delivery.
    /// </summary>
    bool Open(ItemEntity item, MobileEntity reader);
}
