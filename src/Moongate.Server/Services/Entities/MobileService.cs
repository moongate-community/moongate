using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Default mobile service backed by persistence repositories and mobile factory.
/// </summary>
public sealed class MobileService : IMobileService
{
    private readonly IPersistenceService _persistenceService;
    private readonly IMobileFactoryService _mobileFactoryService;

    public MobileService(IPersistenceService persistenceService, IMobileFactoryService mobileFactoryService)
    {
        _persistenceService = persistenceService;
        _mobileFactoryService = mobileFactoryService;
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

        return await _persistenceService.UnitOfWork.Mobiles.RemoveAsync(id, cancellationToken);
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

        return mobile;
    }
}
