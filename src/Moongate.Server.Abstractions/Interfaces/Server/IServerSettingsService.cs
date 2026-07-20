using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Interfaces.Server;

/// <summary>
/// Reads and writes the shard's single server-settings record: the operator-editable profile
/// (description, contacts, web-registration toggle) and the metadata of its visual asset slots.
/// </summary>
public interface IServerSettingsService
{
    /// <summary>Returns the settings, creating the singleton with safe defaults on first access.</summary>
    ServerSettingsEntity Get();

    /// <summary>Applies a partial update; a null field on <paramref name="update" /> is left unchanged.</summary>
    void Update(ServerSettingsUpdate update);

    /// <summary>Records the file metadata for a slot, replacing any previous asset in it.</summary>
    void SetAsset(ServerAssetSlotType slot, ServerAssetMeta meta);

    /// <summary>Removes the asset metadata for a slot, if any.</summary>
    void ClearAsset(ServerAssetSlotType slot);
}
