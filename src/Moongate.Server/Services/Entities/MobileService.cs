using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Default mobile service backed by persistence repositories and mobile factory.
/// </summary>
public sealed class MobileService : IMobileService
{
    private readonly IPersistenceService _persistenceService;
    private readonly IMobileFactoryService _mobileFactoryService;
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly ILuaBrainRunner _luaBrainRunner;

    public MobileService(
        IPersistenceService persistenceService,
        IMobileFactoryService mobileFactoryService,
        IMobileTemplateService mobileTemplateService,
        ILuaBrainRunner luaBrainRunner
    )
    {
        _persistenceService = persistenceService;
        _mobileFactoryService = mobileFactoryService;
        _mobileTemplateService = mobileTemplateService;
        _luaBrainRunner = luaBrainRunner;
    }

    /// <inheritdoc />
    public async Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (mobile.Id == Serial.Zero)
        {
            mobile.Id = _persistenceService.UnitOfWork.AllocateNextMobileId();
        }

        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mobile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
    {
        if (id == Serial.Zero)
        {
            return false;
        }

        var removed = await _persistenceService.UnitOfWork.Mobiles.RemoveAsync(id, cancellationToken);

        if (removed)
        {
            _luaBrainRunner.Unregister(id);
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
    {
        if (id == Serial.Zero)
        {
            return null;
        }

        return await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UOMobileEntity> SpawnFromTemplateAsync(
        string templateId,
        Point3D location,
        int mapId,
        Serial? accountId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        var mobile = _mobileFactoryService.CreateMobileFromTemplate(templateId, accountId);
        mobile.Location = location;
        mobile.MapId = mapId;

        await CreateOrUpdateAsync(mobile, cancellationToken);
        RegisterBrainIfConfigured(templateId, mobile);

        return mobile;
    }

    private void RegisterBrainIfConfigured(string templateId, UOMobileEntity mobile)
    {
        if (!_mobileTemplateService.TryGet(templateId, out var definition) || definition is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.Brain))
        {
            return;
        }

        if (string.Equals(definition.Brain.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _luaBrainRunner.Register(mobile, definition.Brain.Trim());
    }
}
