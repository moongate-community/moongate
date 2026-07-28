using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Subscribers;

/// <summary>Coordinates scheduler bindings with NPC, sector and brain-definition lifecycle events.</summary>
public sealed class NpcBrainLifecycleSubscriber : IEventSubscriberRegistration
{
    private const int SectorShift = 4;

    private readonly ISpatialIndexService _spatial;
    private readonly ISectorActivityService _sectors;
    private readonly INpcBrainScheduler _scheduler;
    private readonly IEntityStore<AccountEntity, Serial> _accounts;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;

    public NpcBrainLifecycleSubscriber(
        ISpatialIndexService spatial,
        IPersistenceService persistence,
        INpcBrainScheduler scheduler,
        ISectorActivityService sectors
    )
    {
        _spatial = spatial;
        _sectors = sectors;
        _scheduler = scheduler;
        _accounts = persistence.GetStore<AccountEntity, Serial>();
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
    }

    public Task OnBrainDefinitionReloaded(
        BrainDefinitionReloadedEvent message,
        CancellationToken cancellationToken
    )
    {
        _scheduler.RefreshDescriptor(message.BrainId, message.Descriptor);

        return Task.CompletedTask;
    }

    public Task OnMobileChangedSector(MobileChangedSectorEvent message, CancellationToken cancellationToken)
    {
        if (IsPlayerCharacter(message.Mobile))
        {
            return Task.CompletedTask;
        }

        if (_sectors.IsActive(message.ToMapId, message.ToSectorX, message.ToSectorY))
        {
            _scheduler.Activate(message.Mobile);
        }
        else
        {
            _scheduler.Deactivate(message.Mobile);
        }

        return Task.CompletedTask;
    }

    public Task OnMobileCreated(MobileCreatedEvent message, CancellationToken cancellationToken)
    {
        if (!IsPlayerCharacter(message.Mobile.Id) && !string.IsNullOrWhiteSpace(message.Mobile.BrainScriptId))
        {
            BindAndActivateWhenSectorIsActive(message.Mobile);
        }

        return Task.CompletedTask;
    }

    public Task OnMobileDeleted(MobileDeletedEvent message, CancellationToken cancellationToken)
    {
        _scheduler.Unbind(message.Mobile.Id);

        return Task.CompletedTask;
    }

    public Task OnSectorActivated(SectorActivatedEvent message, CancellationToken cancellationToken)
    {
        foreach (var mobile in BrainMobilesInSector(message.MapId, message.SectorX, message.SectorY))
        {
            _scheduler.Activate(mobile.Id);
        }

        return Task.CompletedTask;
    }

    public Task OnSectorDeactivated(SectorDeactivatedEvent message, CancellationToken cancellationToken)
    {
        foreach (var mobile in BrainMobilesInSector(message.MapId, message.SectorX, message.SectorY))
        {
            _scheduler.Deactivate(mobile.Id);
        }

        return Task.CompletedTask;
    }

    public Task OnWorldReady(WorldReadyEvent message, CancellationToken cancellationToken)
    {
        var playerCharacters = PlayerCharacters();

        foreach (var mobile in _mobiles.GetAll())
        {
            if (playerCharacters.Contains(mobile.Id) || string.IsNullOrWhiteSpace(mobile.BrainScriptId))
            {
                continue;
            }

            BindAndActivateWhenSectorIsActive(mobile);
        }

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
    {
        eventBus.Subscribe<WorldReadyEvent>(OnWorldReady);
        eventBus.Subscribe<MobileCreatedEvent>(OnMobileCreated);
        eventBus.Subscribe<MobileDeletedEvent>(OnMobileDeleted);
        eventBus.Subscribe<MobileChangedSectorEvent>(OnMobileChangedSector);
        eventBus.Subscribe<SectorActivatedEvent>(OnSectorActivated);
        eventBus.Subscribe<SectorDeactivatedEvent>(OnSectorDeactivated);
        eventBus.Subscribe<BrainDefinitionReloadedEvent>(OnBrainDefinitionReloaded);
    }

    private void BindAndActivateWhenSectorIsActive(MobileEntity mobile)
    {
        _scheduler.Bind(mobile);

        if (_sectors.IsActive(mobile.MapId, mobile.Position.X >> SectorShift, mobile.Position.Y >> SectorShift))
        {
            _scheduler.Activate(mobile.Id);
        }
    }

    private IEnumerable<MobileEntity> BrainMobilesInSector(int mapId, int sectorX, int sectorY)
        => _spatial
           .GetMobilesInSector(mapId, sectorX, sectorY)
           .Where(mobile => !string.IsNullOrWhiteSpace(mobile.BrainScriptId) && !IsPlayerCharacter(mobile.Id))
           .OrderBy(mobile => mobile.Id);

    private bool IsPlayerCharacter(Serial mobileId)
        => _accounts.GetAll().Any(account => account.MobileIds.Contains(mobileId));

    private HashSet<Serial> PlayerCharacters()
        => _accounts.GetAll().SelectMany(account => account.MobileIds).ToHashSet();
}
