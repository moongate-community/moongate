using Moongate.Server.Data.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Items;

/// <summary>
/// Defines item lifecycle and placement operations backed by persistence storage.
/// </summary>
public interface IItemService
{
    /// <summary>
    /// Creates a detached copy of the provided item.
    /// </summary>
    /// <param name="item">Source item entity.</param>
    /// <param name="generateNewSerial">
    /// When <see langword="true" />, allocates a new item serial for the clone; otherwise keeps source serial.
    /// </param>
    /// <returns>A cloned item entity.</returns>
    UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true);

    /// <summary>
    /// Loads an item and creates a detached copy.
    /// </summary>
    /// <param name="itemId">Source item serial identifier.</param>
    /// <param name="generateNewSerial">
    /// When <see langword="true" />, allocates a new item serial for the clone; otherwise keeps source serial.
    /// </param>
    /// <returns>The cloned item when source exists; otherwise <see langword="null" />.</returns>
    Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true);

    /// <summary>
    /// Creates a new item and persists it, allocating a new serial when needed.
    /// </summary>
    /// <param name="item">Item entity to create.</param>
    /// <returns>The created item serial identifier.</returns>
    Task<Serial> CreateItemAsync(UOItemEntity item);

    /// <summary>
    /// Deletes an item by serial identifier.
    /// </summary>
    /// <param name="itemId">Item serial identifier.</param>
    /// <returns><see langword="true" /> when deleted; otherwise <see langword="false" />.</returns>
    Task<bool> DeleteItemAsync(Serial itemId);

    /// <summary>
    /// Drops an item to ground and returns the drop context used for domain events.
    /// </summary>
    /// <param name="itemId">Item serial identifier.</param>
    /// <param name="location">Target world location.</param>
    /// <param name="mapId">Target map id.</param>
    /// <returns>Drop context when operation succeeds; otherwise <see langword="null" />.</returns>
    Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId);

    /// <summary>
    /// Equips an item on a mobile at the specified layer.
    /// </summary>
    /// <param name="itemId">Item serial identifier.</param>
    /// <param name="mobileId">Mobile serial identifier.</param>
    /// <param name="layer">Target equipment layer.</param>
    /// <returns><see langword="true" /> when operation succeeds; otherwise <see langword="false" />.</returns>
    Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer);

    /// <summary>
    /// Loads ground items persisted in the specified map sector.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="sectorX">Sector X coordinate.</param>
    /// <param name="sectorY">Sector Y coordinate.</param>
    /// <returns>Ground items for the sector.</returns>
    Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY);

    /// <summary>
    /// Loads an item entity by serial identifier.
    /// </summary>
    /// <param name="itemId">Item serial identifier.</param>
    /// <returns>The item entity when found; otherwise <see langword="null" />.</returns>
    Task<UOItemEntity?> GetItemAsync(Serial itemId);

    /// <summary>
    /// Loads all items contained by the specified container serial.
    /// </summary>
    /// <param name="containerId">Container item serial identifier.</param>
    /// <returns>List of contained item entities.</returns>
    Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId);

    /// <summary>
    /// Moves an item into a container at a specific container-local position.
    /// </summary>
    /// <param name="itemId">Item serial identifier.</param>
    /// <param name="containerId">Target container serial identifier.</param>
    /// <param name="position">Position inside target container.</param>
    /// <returns><see langword="true" /> when operation succeeds; otherwise <see langword="false" />.</returns>
    Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position);

    /// <summary>
    /// Moves an item to world coordinates, detaching it from containers and equipment.
    /// </summary>
    /// <param name="itemId">Item serial identifier.</param>
    /// <param name="location">Target world location.</param>
    /// <param name="mapId">Target map id.</param>
    /// <returns><see langword="true" /> when operation succeeds; otherwise <see langword="false" />.</returns>
    Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId);

    /// <summary>
    /// Inserts or updates an existing item.
    /// </summary>
    /// <param name="item">Item entity to persist.</param>
    Task UpsertItemAsync(UOItemEntity item);

    /// <summary>
    /// Inserts or updates multiple items in sequence.
    /// </summary>
    /// <param name="items">Item entities to persist.</param>
    Task UpsertItemsAsync(params UOItemEntity[] items);
}
