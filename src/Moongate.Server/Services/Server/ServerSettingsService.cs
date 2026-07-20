using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Server;
using Moongate.Server.Abstractions.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Server;

public sealed class ServerSettingsService : IServerSettingsService
{
    private static readonly Serial SettingsId = new(1);

    private readonly IEntityStore<ServerSettingsEntity, Serial> _store;

    public ServerSettingsService(IPersistenceService persistenceService)
    {
        _store = persistenceService.GetStore<ServerSettingsEntity, Serial>();
    }

    public ServerSettingsEntity Get()
    {
        if (_store.Query().FirstOrDefault(entity => entity.Id == SettingsId) is { } existing)
        {
            return existing;
        }

        var created = new ServerSettingsEntity { Id = SettingsId };
        _store.UpsertAsync(created).WaitSync();

        return created;
    }

    public void Update(ServerSettingsUpdate update)
    {
        var settings = Get();

        if (update.Description is not null)
        {
            settings.Description = update.Description;
        }

        if (update.Contacts is not null)
        {
            settings.Contacts = update.Contacts;
        }

        if (update.RegistrationEnabled is { } enabled)
        {
            settings.RegistrationEnabled = enabled;
        }

        _store.UpsertAsync(settings).WaitSync();
    }

    public void SetAsset(ServerAssetSlotType slot, ServerAssetMeta meta)
    {
        var settings = Get();
        settings.Assets[slot.ToString()] = meta;
        _store.UpsertAsync(settings).WaitSync();
    }

    public void ClearAsset(ServerAssetSlotType slot)
    {
        var settings = Get();

        if (settings.Assets.Remove(slot.ToString()))
        {
            _store.UpsertAsync(settings).WaitSync();
        }
    }
}
