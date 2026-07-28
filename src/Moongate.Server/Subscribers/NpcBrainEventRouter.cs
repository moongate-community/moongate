using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Subscribers;

/// <summary>Routes canonical world events into active NPC brain mailboxes.</summary>
public sealed class NpcBrainEventRouter : IEventSubscriberRegistration
{
    private const int SectorShift = 4;

    private readonly ISpatialIndexService _spatial;
    private readonly ISessionManager _sessions;
    private readonly INpcBrainScheduler _scheduler;
    private readonly ISectorActivityService _sectors;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly Dictionary<Serial, HashSet<Serial>> _perceivedByObserver = [];

    public NpcBrainEventRouter(
        ISpatialIndexService spatial,
        IPersistenceService persistence,
        ISessionManager sessions,
        INpcBrainScheduler scheduler,
        ISectorActivityService sectors
    )
    {
        _spatial = spatial;
        _sessions = sessions;
        _scheduler = scheduler;
        _sectors = sectors;
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
    }

    public Task OnBrainDefinitionReloaded(
        BrainDefinitionReloadedEvent message,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(
                message.BrainId,
                message.Descriptor.BrainId,
                StringComparison.Ordinal
            ))
        {
            return Task.CompletedTask;
        }

        foreach (var observer in _mobiles
                                 .GetAll()
                                 .Where(
                                     mobile =>
                                         string.Equals(
                                             mobile.BrainScriptId,
                                             message.BrainId,
                                             StringComparison.Ordinal
                                         )
                                 )
                                 .OrderBy(mobile => mobile.Id))
        {
            if (_scheduler.IsActive(observer.Id))
            {
                ReconcileObserverSet(observer, message.Descriptor);

                continue;
            }

            _perceivedByObserver.Remove(observer.Id);
        }

        return Task.CompletedTask;
    }

    public Task OnMobileAttacked(
        MobileAttackedEvent message,
        CancellationToken cancellationToken
    )
    {
        RouteCombatEvent(
            message.Defender,
            message.Attacker,
            NpcBrainHookType.Attacked,
            NpcBrainEventType.Attacked
        );

        return Task.CompletedTask;
    }

    public Task OnMobileCreated(
        MobileCreatedEvent message,
        CancellationToken cancellationToken
    )
    {
        RouteSubjectArrival(message.Mobile);

        if (!string.IsNullOrWhiteSpace(message.Mobile.BrainScriptId))
        {
            RefreshObserverSet(message.Mobile);
        }

        return Task.CompletedTask;
    }

    public Task OnMobileDamaged(
        MobileDamagedEvent message,
        CancellationToken cancellationToken
    )
    {
        RouteCombatEvent(
            message.Mobile,
            message.Attacker,
            NpcBrainHookType.Damage,
            NpcBrainEventType.Damage,
            message.Amount
        );

        return Task.CompletedTask;
    }

    public Task OnMobileDeleted(
        MobileDeletedEvent message,
        CancellationToken cancellationToken
    )
    {
        RemoveSubject(message.Mobile);
        _perceivedByObserver.Remove(message.Mobile.Id);

        return Task.CompletedTask;
    }

    public Task OnMobileDied(
        MobileDiedEvent message,
        CancellationToken cancellationToken
    )
    {
        RouteCombatEvent(
            message.Mobile,
            message.Killer,
            NpcBrainHookType.Death,
            NpcBrainEventType.Death
        );

        return Task.CompletedTask;
    }

    public Task OnMobileMoved(
        MobileMovedEvent message,
        CancellationToken cancellationToken
    )
    {
        if (_mobiles.GetById(message.Mobile) is not { } subject)
        {
            return Task.CompletedTask;
        }

        RouteSubjectMovement(subject, message);

        if (!string.IsNullOrWhiteSpace(subject.BrainScriptId))
        {
            RefreshObserverSet(subject);
        }

        return Task.CompletedTask;
    }

    public Task OnMobileSpeech(
        MobileSpeechEvent message,
        CancellationToken cancellationToken
    )
    {
        if (_mobiles.GetById(message.Speaker) is not { } speaker)
        {
            return Task.CompletedTask;
        }

        var snapshot = ToSnapshot(speaker);

        foreach (var observer in NearbyObservers(
                     speaker.MapId,
                     speaker.Position,
                     _scheduler.MaxHearingRange
                 ))
        {
            if (
                observer.Id == speaker.Id ||
                !TryGetActiveDescriptor(observer.Id, out var descriptor) ||
                !observer.Position.InRange(speaker.Position, descriptor.HearingRange)
            )
            {
                continue;
            }

            _scheduler.EnqueueEvent(
                observer.Id,
                NpcBrainHookType.SpeechHeard,
                new(
                    NpcBrainEventType.SpeechHeard,
                    snapshot,
                    message.Text,
                    message.Type
                )
            );
        }

        return Task.CompletedTask;
    }

    public Task OnPlayerEnteredWorld(
        PlayerEnteredWorldEvent message,
        CancellationToken cancellationToken
    )
    {
        RouteSubjectArrival(message.Mobile, true);

        return Task.CompletedTask;
    }

    public Task OnSectorActivated(
        SectorActivatedEvent message,
        CancellationToken cancellationToken
    )
    {
        foreach (var mobile in _spatial
                               .GetMobilesInSector(message.MapId, message.SectorX, message.SectorY)
                               .Where(mobile => !string.IsNullOrWhiteSpace(mobile.BrainScriptId))
                               .OrderBy(mobile => mobile.Id))
        {
            SeedObserver(mobile);
        }

        return Task.CompletedTask;
    }

    public Task OnSectorDeactivated(
        SectorDeactivatedEvent message,
        CancellationToken cancellationToken
    )
    {
        foreach (var mobile in _spatial
                               .GetMobilesInSector(message.MapId, message.SectorX, message.SectorY)
                               .Where(mobile => !string.IsNullOrWhiteSpace(mobile.BrainScriptId))
                               .OrderBy(mobile => mobile.Id))
        {
            _perceivedByObserver.Remove(mobile.Id);
        }

        return Task.CompletedTask;
    }

    public Task OnSessionDestroyed(
        SessionDestroyedEvent message,
        CancellationToken cancellationToken
    )
    {
        if (message.Session.Character is { } character)
        {
            RemoveSubject(character, true);
            _perceivedByObserver.Remove(character.Id);
        }

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
    {
        eventBus.Subscribe<MobileSpeechEvent>(OnMobileSpeech);
        eventBus.Subscribe<MobileMovedEvent>(OnMobileMoved);
        eventBus.Subscribe<MobileCreatedEvent>(OnMobileCreated);
        eventBus.Subscribe<MobileDeletedEvent>(OnMobileDeleted);
        eventBus.Subscribe<PlayerEnteredWorldEvent>(OnPlayerEnteredWorld);
        eventBus.Subscribe<SessionDestroyedEvent>(OnSessionDestroyed);
        eventBus.Subscribe<SectorActivatedEvent>(OnSectorActivated);
        eventBus.Subscribe<SectorDeactivatedEvent>(OnSectorDeactivated);
        eventBus.Subscribe<BrainDefinitionReloadedEvent>(OnBrainDefinitionReloaded);
        eventBus.Subscribe<MobileAttackedEvent>(OnMobileAttacked);
        eventBus.Subscribe<MobileDamagedEvent>(OnMobileDamaged);
        eventBus.Subscribe<MobileDiedEvent>(OnMobileDied);
    }

    private void EnqueuePerception(
        Serial observerId,
        NpcBrainHookType hook,
        NpcBrainEventType eventType,
        BrainMobileSnapshot snapshot,
        MobileMovedEvent movement
    )
        => _scheduler.EnqueueEvent(
            observerId,
            hook,
            new(
                eventType,
                snapshot,
                FromPosition: movement.FromPosition,
                ToPosition: movement.ToPosition
            )
        );

    private HashSet<Serial> GetPerceived(Serial observerId)
    {
        if (!_perceivedByObserver.TryGetValue(observerId, out var perceived))
        {
            perceived = [];
            _perceivedByObserver[observerId] = perceived;
        }

        return perceived;
    }

    private IEnumerable<MobileEntity> NearbyObservers(
        int mapId,
        Point3D position,
        int range
    )
        => _spatial
           .GetMobilesInRange(mapId, position, range)
           .OrderBy(mobile => mobile.Id);

    private void ReconcileObserverSet(
        MobileEntity observer,
        BrainDescriptor descriptor
    )
    {
        var current = _spatial
                      .GetMobilesInRange(
                          observer.MapId,
                          observer.Position,
                          descriptor.PerceptionRange
                      )
                      .Where(subject => subject.Id != observer.Id)
                      .OrderBy(subject => subject.Id)
                      .ToArray();
        var previous = _perceivedByObserver.TryGetValue(observer.Id, out var perceived)
                           ? perceived
                           : [];
        var currentIds = current.Select(subject => subject.Id).ToHashSet();

        foreach (var subjectId in previous.Except(currentIds).Order())
        {
            if (_mobiles.GetById(subjectId) is { } subject)
            {
                _scheduler.EnqueueEvent(
                    observer.Id,
                    NpcBrainHookType.MobileLeftRange,
                    new(
                        NpcBrainEventType.MobileLeftRange,
                        ToSnapshot(subject),
                        FromPosition: subject.Position,
                        ToPosition: subject.Position
                    )
                );
            }
        }

        foreach (var subject in current.Where(subject => !previous.Contains(subject.Id)))
        {
            _scheduler.EnqueueEvent(
                observer.Id,
                NpcBrainHookType.MobileEnteredRange,
                new(
                    NpcBrainEventType.MobileEnteredRange,
                    ToSnapshot(subject),
                    FromPosition: subject.Position,
                    ToPosition: subject.Position
                )
            );
        }

        _perceivedByObserver[observer.Id] = currentIds;
    }

    private void RefreshObserverSet(MobileEntity observer)
    {
        if (
            _scheduler.IsActive(observer.Id) &&
            _sectors.IsActive(
                observer.MapId,
                observer.Position.X >> SectorShift,
                observer.Position.Y >> SectorShift
            )
        )
        {
            SeedObserver(observer);

            return;
        }

        _perceivedByObserver.Remove(observer.Id);
    }

    private void RemoveSubject(MobileEntity subject, bool? isPlayer = null)
    {
        var snapshot = ToSnapshot(subject, isPlayer);

        foreach (var (observerId, perceived) in _perceivedByObserver.OrderBy(pair => pair.Key))
        {
            if (!perceived.Remove(subject.Id) || !_scheduler.IsActive(observerId))
            {
                continue;
            }

            _scheduler.EnqueueEvent(
                observerId,
                NpcBrainHookType.MobileLeftRange,
                new(
                    NpcBrainEventType.MobileLeftRange,
                    snapshot,
                    FromPosition: subject.Position,
                    ToPosition: subject.Position
                )
            );
        }
    }

    private void RouteCombatEvent(
        Serial affected,
        Serial other,
        NpcBrainHookType hook,
        NpcBrainEventType eventType,
        int amount = 0
    )
    {
        if (!_scheduler.IsActive(affected))
        {
            return;
        }

        var otherSnapshot = _mobiles.GetById(other) is { } mobile
                                ? ToSnapshot(mobile)
                                : null;
        _scheduler.EnqueueEvent(
            affected,
            hook,
            new(eventType, otherSnapshot, Amount: amount)
        );
    }

    private void RouteSubjectArrival(MobileEntity subject, bool? isPlayer = null)
    {
        var snapshot = ToSnapshot(subject, isPlayer);

        foreach (var observer in NearbyObservers(
                     subject.MapId,
                     subject.Position,
                     _scheduler.MaxPerceptionRange
                 ))
        {
            if (
                observer.Id == subject.Id ||
                !TryGetActiveDescriptor(observer.Id, out var descriptor) ||
                !observer.Position.InRange(subject.Position, descriptor.PerceptionRange)
            )
            {
                continue;
            }

            if (!GetPerceived(observer.Id).Add(subject.Id))
            {
                continue;
            }

            _scheduler.EnqueueEvent(
                observer.Id,
                NpcBrainHookType.MobileEnteredRange,
                new(
                    NpcBrainEventType.MobileEnteredRange,
                    snapshot,
                    FromPosition: subject.Position,
                    ToPosition: subject.Position
                )
            );
        }
    }

    private void RouteSubjectMovement(MobileEntity subject, MobileMovedEvent movement)
    {
        var candidateIds = _spatial
                           .GetMobilesInRange(
                               movement.FromMapId,
                               movement.FromPosition,
                               _scheduler.MaxPerceptionRange
                           )
                           .Concat(
                               _spatial.GetMobilesInRange(
                                   movement.ToMapId,
                                   movement.ToPosition,
                                   _scheduler.MaxPerceptionRange
                               )
                           )
                           .Select(mobile => mobile.Id)
                           .Concat(
                               _perceivedByObserver
                                   .Where(pair => pair.Value.Contains(subject.Id))
                                   .Select(pair => pair.Key)
                           )
                           .Where(observerId => observerId != subject.Id)
                           .Distinct()
                           .Order()
                           .ToArray();
        var snapshot = ToSnapshot(subject);

        foreach (var observerId in candidateIds)
        {
            if (_mobiles.GetById(observerId) is not { } observer ||
                !TryGetActiveDescriptor(observerId, out var descriptor))
            {
                _perceivedByObserver.Remove(observerId);

                continue;
            }

            var wasPerceived = _perceivedByObserver.TryGetValue(
                                   observerId,
                                   out var perceived
                               ) &&
                               perceived.Contains(subject.Id);
            var isInside = observer.MapId == movement.ToMapId &&
                           observer.Position.InRange(
                               movement.ToPosition,
                               descriptor.PerceptionRange
                           );

            if (!wasPerceived && !isInside)
            {
                continue;
            }

            if (!wasPerceived)
            {
                perceived = GetPerceived(observerId);
                perceived.Add(subject.Id);
                EnqueuePerception(
                    observerId,
                    NpcBrainHookType.MobileEnteredRange,
                    NpcBrainEventType.MobileEnteredRange,
                    snapshot,
                    movement
                );

                continue;
            }

            if (isInside)
            {
                EnqueuePerception(
                    observerId,
                    NpcBrainHookType.MobileMoved,
                    NpcBrainEventType.MobileMoved,
                    snapshot,
                    movement
                );

                continue;
            }

            _perceivedByObserver[observerId].Remove(subject.Id);
            EnqueuePerception(
                observerId,
                NpcBrainHookType.MobileLeftRange,
                NpcBrainEventType.MobileLeftRange,
                snapshot,
                movement
            );
        }
    }

    private void SeedObserver(MobileEntity observer)
    {
        if (!TryGetActiveDescriptor(observer.Id, out var descriptor))
        {
            _perceivedByObserver.Remove(observer.Id);

            return;
        }

        ReconcileObserverSet(observer, descriptor);
    }

    private BrainMobileSnapshot ToSnapshot(
        MobileEntity mobile,
        bool? isPlayer = null
    )
        => new(
            mobile.Id,
            mobile.Name,
            isPlayer ?? _sessions.IsCharacterPlayed(mobile.Id),
            mobile.MapId,
            mobile.Position,
            mobile.Hits,
            mobile.HitsMax,
            mobile.Warmode,
            mobile.CombatantId,
            mobile.Criminal,
            mobile.Kills
        );

    private bool TryGetActiveDescriptor(
        Serial observerId,
        [NotNullWhen(true)] out BrainDescriptor? descriptor
    )
    {
        if (
            _scheduler.IsActive(observerId) &&
            _scheduler.TryGetDescriptor(observerId, out var found) &&
            found is not null
        )
        {
            descriptor = found;

            return true;
        }

        descriptor = null;

        return false;
    }
}
