using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Data.Internal.AI;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using ILogger = Serilog.ILogger;

namespace Moongate.Server.Services.AI;

public sealed class NpcBrainScheduler : INpcBrainScheduler, ISquidStdService
{
    private const string TimerName = "npc-brain-scheduler";
    private const int SectorShift = 4;
    private const int FaultLogIntervalSeconds = 60;
    private const int QueueCompactionFactor = 2;
    private const int QueueCompactionSlack = 8;

    private readonly ILogger _logger = Log.ForContext<NpcBrainScheduler>();
    private readonly IGameLoopContext _loop;
    private readonly INpcBrainRuntime _runtime;
    private readonly IBrainIntentExecutor _intentExecutor;
    private readonly ISectorActivityService _sectors;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly NpcBrainContextFactory _contextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly NpcAiAdvancedConfig _advanced;
    private readonly INpcAiMetrics _metrics;
    private readonly Dictionary<Serial, SchedulerEntry> _entries = [];
    private readonly PriorityQueue<(Serial MobileId, long Version), (long DueTicks, long Sequence)> _queue = new();
    private readonly Dictionary<
        (string BrainId, Serial MobileId, NpcBrainHookType Hook, string Error),
        DateTimeOffset
    > _faultLogs = [];

    private string? _timerId;
    private long _nextQueueSequence;

    public int MaxHearingRange => _entries.Values
        .Where(HasCurrentDescriptor)
        .Select(entry => entry.Descriptor!.HearingRange)
        .DefaultIfEmpty()
        .Max();

    public int MaxPerceptionRange => _entries.Values
        .Where(HasCurrentDescriptor)
        .Select(entry => entry.Descriptor!.PerceptionRange)
        .DefaultIfEmpty()
        .Max();

    public NpcBrainScheduler(
        IGameLoopContext loop,
        INpcBrainRuntime runtime,
        IBrainIntentExecutor intentExecutor,
        ISectorActivityService sectors,
        IPersistenceService persistenceService,
        NpcBrainContextFactory contextFactory,
        TimeProvider timeProvider,
        MoongateConfig config,
        INpcAiMetrics metrics
    )
    {
        _loop = loop;
        _runtime = runtime;
        _intentExecutor = intentExecutor;
        _sectors = sectors;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
        _advanced = config.NpcAi.Advanced;
        _metrics = metrics;
    }

    public void Bind(MobileEntity mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile.BrainScriptId))
        {
            Unbind(mobile.Id);
            return;
        }

        if (_entries.TryGetValue(mobile.Id, out var existing))
        {
            if (string.Equals(existing.BrainId, mobile.BrainScriptId, StringComparison.Ordinal))
            {
                if (_runtime.TryGetDescriptor(mobile.Id, out var currentDescriptor) &&
                    !TryCacheDescriptor(existing, currentDescriptor))
                {
                    existing.Descriptor = null;
                }

                return;
            }

            RebindChangedBrain(existing, mobile);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var isActive = IsSectorActive(mobile);
        var entry = new SchedulerEntry(
            mobile.Id,
            mobile.BrainScriptId,
            isActive ? NpcBrainStateType.Active : NpcBrainStateType.Sleeping,
            mobile.MapId,
            mobile.Position,
            now,
            new NpcBrainMailbox(_advanced.MaxMailboxEvents, _metrics)
        );
        _entries[mobile.Id] = entry;

        if (isActive)
        {
            entry.Mailbox.Enqueue(
                NpcBrainHookType.Activate,
                new NpcBrainEvent(NpcBrainEventType.Activate)
            );
        }

        var isBound = _runtime.TryBind(
            mobile.Id,
            mobile.BrainScriptId,
            out var descriptor,
            out var error
        ) && TryCacheDescriptor(entry, descriptor);

        if (!isBound && isActive)
        {
            RegisterFailure(entry, NpcBrainHookType.Activate, error ?? "Brain binding failed.", now);
        }

        if (entry.State != NpcBrainStateType.Sleeping)
        {
            Requeue(entry, GetDueAt(entry, now));
        }
    }

    public void Unbind(Serial mobileId)
    {
        ClearFaultLogs(mobileId);

        if (!_entries.Remove(mobileId))
        {
            return;
        }

        _runtime.Unbind(mobileId);
        CompactQueueIfNeeded();
    }

    public void Activate(Serial mobileId)
    {
        if (!_entries.TryGetValue(mobileId, out var entry))
        {
            return;
        }

        if (entry.State == NpcBrainStateType.Deactivating)
        {
            entry.State = NpcBrainStateType.Active;
            entry.Mailbox.Clear();
            Requeue(entry, entry.NextThinkAt, true);
            return;
        }

        if (entry.State != NpcBrainStateType.Sleeping)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        entry.State = NpcBrainStateType.Active;
        entry.NextThinkAt = now;
        entry.FaultUntil = null;
        entry.ConsecutiveFailures = 0;
        entry.Mailbox.Enqueue(
            NpcBrainHookType.Activate,
            new NpcBrainEvent(NpcBrainEventType.Activate)
        );
        Requeue(entry, now);
    }

    public void Deactivate(Serial mobileId)
    {
        if (!_entries.TryGetValue(mobileId, out var entry) ||
            entry.State is NpcBrainStateType.Sleeping or NpcBrainStateType.Deactivating)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        entry.State = NpcBrainStateType.Deactivating;
        entry.FaultUntil = null;
        entry.Mailbox.Clear();
        entry.Mailbox.Enqueue(
            NpcBrainHookType.Deactivate,
            new NpcBrainEvent(NpcBrainEventType.Deactivate)
        );
        Requeue(entry, now);
    }

    public void RefreshDescriptor(string brainId, BrainDescriptor descriptor)
    {
        if (!string.Equals(descriptor.BrainId, brainId, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var entry in _entries.Values.Where(
                     entry => string.Equals(entry.BrainId, brainId, StringComparison.Ordinal)
                 ))
        {
            TryCacheDescriptor(entry, descriptor);
        }
    }

    public bool IsActive(Serial mobileId)
        => _entries.TryGetValue(mobileId, out var entry) &&
           entry.State == NpcBrainStateType.Active;

    public bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor)
    {
        if (_entries.TryGetValue(mobileId, out var entry) && HasCurrentDescriptor(entry))
        {
            descriptor = entry.Descriptor;
            return true;
        }

        descriptor = null;
        return false;
    }

    public void EnqueueEvent(Serial mobileId, NpcBrainHookType hook, NpcBrainEvent brainEvent)
    {
        if (!_entries.TryGetValue(mobileId, out var entry) ||
            entry.State is NpcBrainStateType.Sleeping or NpcBrainStateType.Deactivating)
        {
            return;
        }

        var schedulingChanged = entry.Mailbox.Enqueue(hook, brainEvent);

        if (entry.State == NpcBrainStateType.Active && schedulingChanged)
        {
            Requeue(entry, _timeProvider.GetUtcNow());
        }
    }

    public void Tick()
    {
        var now = _timeProvider.GetUtcNow();
        PruneFaultLogs(now);
        var executed = 0;
        var woken = new HashSet<Serial>();
        var postponed = new List<(
            (Serial MobileId, long Version) Element,
            (long DueTicks, long Sequence) Priority
        )>();

        while (executed < _advanced.MaxBrainsPerLoop &&
               _queue.TryPeek(out _, out var priority) &&
               priority.DueTicks <= DueTicks(now))
        {
            _queue.TryDequeue(out var element, out priority);
            var (mobileId, version) = element;

            if (!_entries.TryGetValue(mobileId, out var entry) ||
                entry.QueueVersion != version)
            {
                continue;
            }

            if (entry.State == NpcBrainStateType.Sleeping)
            {
                entry.ScheduledDueTicks = null;
                continue;
            }

            if (woken.Contains(mobileId))
            {
                postponed.Add((element, priority));
                continue;
            }

            entry.ScheduledDueTicks = null;
            var dueAt = GetDueAt(entry, now);

            if (dueAt > now)
            {
                Requeue(entry, dueAt);
                continue;
            }

            woken.Add(mobileId);
            executed++;
            _metrics.RecordBrainExecuted();
            ProcessEntry(entry, now);

            if (entry.State != NpcBrainStateType.Sleeping)
            {
                Requeue(entry, GetDueAt(entry, now));
            }
        }

        foreach (var queued in postponed)
        {
            _queue.Enqueue(queued.Element, queued.Priority);
        }

        CompactQueueIfNeeded();

        if (executed == _advanced.MaxBrainsPerLoop)
        {
            RecordDeferred(now, woken);
        }

        UpdateGauges();
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_timerId is null)
        {
            _timerId = _loop.ScheduleRepeating(
                TimerName,
                TimeSpan.FromMilliseconds(_advanced.MinTickMilliseconds),
                Tick
            );
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_timerId is not null)
        {
            _loop.Cancel(_timerId);
            _timerId = null;
        }

        return ValueTask.CompletedTask;
    }

    private void RebindChangedBrain(SchedulerEntry entry, MobileEntity mobile)
    {
        var now = _timeProvider.GetUtcNow();
        ClearFaultLogs(mobile.Id);
        CancelSchedule(entry);
        _runtime.Reset(mobile.Id);
        entry.BrainId = mobile.BrainScriptId;
        entry.Descriptor = null;
        entry.State = IsSectorActive(mobile) ? NpcBrainStateType.Active : NpcBrainStateType.Sleeping;
        entry.HomeMapId = mobile.MapId;
        entry.HomePosition = mobile.Position;
        entry.NextThinkAt = now;
        entry.FaultUntil = null;
        entry.ConsecutiveFailures = 0;
        entry.Mailbox.Clear();

        if (entry.State == NpcBrainStateType.Active)
        {
            entry.Mailbox.Enqueue(
                NpcBrainHookType.Activate,
                new NpcBrainEvent(NpcBrainEventType.Activate)
            );
        }

        var isBound = _runtime.TryBind(
            mobile.Id,
            mobile.BrainScriptId,
            out var descriptor,
            out var error
        ) && TryCacheDescriptor(entry, descriptor);

        if (!isBound && entry.State == NpcBrainStateType.Active)
        {
            RegisterFailure(entry, NpcBrainHookType.Activate, error ?? "Brain binding failed.", now);
        }

        if (entry.State != NpcBrainStateType.Sleeping)
        {
            Requeue(entry, GetDueAt(entry, now));
        }
    }

    private void ProcessEntry(SchedulerEntry entry, DateTimeOffset now)
    {
        if (entry.State == NpcBrainStateType.FaultBackoff)
        {
            if (entry.FaultUntil is { } faultUntil && faultUntil > now)
            {
                return;
            }

            entry.State = NpcBrainStateType.Active;
            entry.FaultUntil = null;
        }

        var owner = _mobiles.GetById(entry.MobileId);

        if (owner is null)
        {
            if (entry.State == NpcBrainStateType.Deactivating)
            {
                CompleteSleep(entry);
                return;
            }

            RegisterFailure(entry, NpcBrainHookType.Think, "Brain owner is missing.", now);
            return;
        }

        if (!EnsureBinding(entry, now))
        {
            if (entry.State == NpcBrainStateType.Deactivating)
            {
                CompleteSleep(entry);
            }

            return;
        }

        var context = _contextFactory.Create(
            owner,
            entry.HomeMapId,
            entry.HomePosition,
            entry.Descriptor!
        );

        if (entry.State == NpcBrainStateType.Deactivating)
        {
            ProcessDeactivation(entry, context, now);
            return;
        }

        for (var delivered = 0;
             delivered < _advanced.MaxEventsPerBrainWake && entry.Mailbox.HasEvents;
             delivered++)
        {
            var queued = entry.Mailbox.Dequeue(1)[0];
            var result = Invoke(entry, queued.Hook, context, queued.Event);

            if (!result.Success)
            {
                RegisterFailure(
                    entry,
                    queued.Hook,
                    result.Error ?? "Brain hook failed.",
                    now
                );
                return;
            }

            ApplySuccess(entry, context, result.Decision, false, now);
        }

        if (entry.NextThinkAt > now)
        {
            return;
        }

        var thinkResult = Invoke(entry, NpcBrainHookType.Think, context, null);

        if (!thinkResult.Success)
        {
            RegisterFailure(
                entry,
                NpcBrainHookType.Think,
                thinkResult.Error ?? "Brain think failed.",
                now
            );
            return;
        }

        ApplySuccess(entry, context, thinkResult.Decision, true, now);
    }

    private bool EnsureBinding(SchedulerEntry entry, DateTimeOffset now)
    {
        if (_runtime.TryGetDescriptor(entry.MobileId, out var descriptor) &&
            TryCacheDescriptor(entry, descriptor))
        {
            return true;
        }

        if (_runtime.TryBind(entry.MobileId, entry.BrainId, out descriptor, out var error) &&
            TryCacheDescriptor(entry, descriptor))
        {
            entry.ConsecutiveFailures = 0;
            return true;
        }

        if (entry.State != NpcBrainStateType.Deactivating)
        {
            RegisterFailure(
                entry,
                NpcBrainHookType.Think,
                error ?? "Brain binding failed.",
                now
            );
        }
        else
        {
            LogFailure(entry, NpcBrainHookType.Deactivate, error ?? "Brain binding failed.", now);
        }

        return false;
    }

    private void ProcessDeactivation(SchedulerEntry entry, BrainContext context, DateTimeOffset now)
    {
        if (entry.Mailbox.HasEvents)
        {
            var queued = entry.Mailbox.Dequeue(1)[0];
            var result = Invoke(entry, queued.Hook, context, queued.Event);

            if (result.Success)
            {
                ApplySuccess(entry, context, result.Decision, false, now);
            }
            else
            {
                LogFailure(
                    entry,
                    queued.Hook,
                    result.Error ?? "Brain deactivation failed.",
                    now
                );
            }
        }

        CompleteSleep(entry);
    }

    private NpcBrainInvocationResult Invoke(
        SchedulerEntry entry,
        NpcBrainHookType hook,
        BrainContext context,
        NpcBrainEvent? brainEvent
    )
    {
        var started = _timeProvider.GetTimestamp();

        try
        {
            return _runtime.Invoke(entry.MobileId, hook, context, brainEvent);
        }
        catch (Exception exception)
        {
            _metrics.RecordHookFailure(false);
            return NpcBrainInvocationResult.Failed(exception.Message);
        }
        finally
        {
            _metrics.RecordHookInvocation(_timeProvider.GetElapsedTime(started));
        }
    }

    private void ApplySuccess(
        SchedulerEntry entry,
        BrainContext context,
        BrainDecision decision,
        bool wasThink,
        DateTimeOffset now
    )
    {
        entry.ConsecutiveFailures = 0;
        entry.FaultUntil = null;

        if (decision.Intents.Count > 0)
        {
            _intentExecutor.Execute(entry.MobileId, context, decision.Intents);
        }

        if (decision.NextTickMilliseconds is { } nextTick)
        {
            entry.NextThinkAt = now + TimeSpan.FromMilliseconds(
                Math.Clamp(
                    nextTick,
                    _advanced.MinTickMilliseconds,
                    _advanced.MaxTickMilliseconds
                )
            );
        }
        else if (wasThink)
        {
            entry.NextThinkAt = now + TimeSpan.FromMilliseconds(
                Math.Clamp(
                    entry.Descriptor!.DefaultTickMilliseconds,
                    _advanced.MinTickMilliseconds,
                    _advanced.MaxTickMilliseconds
                )
            );
        }
    }

    private void RegisterFailure(
        SchedulerEntry entry,
        NpcBrainHookType hook,
        string error,
        DateTimeOffset now
    )
    {
        entry.ConsecutiveFailures++;

        if (entry.ConsecutiveFailures == 4)
        {
            _runtime.Reset(entry.MobileId);
        }

        entry.State = NpcBrainStateType.FaultBackoff;
        entry.FaultUntil = now + FailureBackoff(entry.ConsecutiveFailures);
        LogFailure(entry, hook, error, now);
    }

    private void LogFailure(
        SchedulerEntry entry,
        NpcBrainHookType hook,
        string error,
        DateTimeOffset now
    )
    {
        PruneFaultLogs(now);
        var key = (entry.BrainId, entry.MobileId, hook, error);

        if (_faultLogs.TryGetValue(key, out var loggedAt) &&
            now - loggedAt < TimeSpan.FromSeconds(FaultLogIntervalSeconds))
        {
            return;
        }

        _faultLogs[key] = now;
        _logger.Warning(
            "NPC brain scheduler fault for brain {BrainId}, mobile {MobileId}, hook {Hook}: {Error}",
            entry.BrainId,
            entry.MobileId.Value,
            hook,
            error
        );
    }

    private void ClearFaultLogs(Serial mobileId)
    {
        foreach (var key in _faultLogs.Keys.Where(key => key.MobileId == mobileId).ToArray())
        {
            _faultLogs.Remove(key);
        }
    }

    private void CompleteSleep(SchedulerEntry entry)
    {
        CancelSchedule(entry);
        entry.State = NpcBrainStateType.Sleeping;
        entry.FaultUntil = null;
        entry.ConsecutiveFailures = 0;
        entry.Mailbox.Clear();
    }

    private void Requeue(
        SchedulerEntry entry,
        DateTimeOffset dueAt,
        bool replaceExisting = false
    )
    {
        var dueTicks = DueTicks(dueAt);

        if (!replaceExisting &&
            entry.ScheduledDueTicks is { } scheduledDueTicks &&
            scheduledDueTicks <= dueTicks)
        {
            return;
        }

        entry.QueueVersion++;
        entry.ScheduledDueTicks = dueTicks;
        _queue.Enqueue(
            (entry.MobileId, entry.QueueVersion),
            (dueTicks, _nextQueueSequence++)
        );
        CompactQueueIfNeeded();
    }

    private void CancelSchedule(SchedulerEntry entry)
    {
        if (entry.ScheduledDueTicks is null)
        {
            return;
        }

        entry.QueueVersion++;
        entry.ScheduledDueTicks = null;
    }

    private void CompactQueueIfNeeded()
    {
        var scheduledCount = _entries.Values.Count(entry => entry.ScheduledDueTicks is not null);
        var maximumQueueCount = (scheduledCount * QueueCompactionFactor) + QueueCompactionSlack;

        if (_queue.Count <= maximumQueueCount)
        {
            return;
        }

        var current = _queue.UnorderedItems
            .Where(item =>
                _entries.TryGetValue(item.Element.MobileId, out var entry) &&
                entry.QueueVersion == item.Element.Version &&
                entry.ScheduledDueTicks == item.Priority.DueTicks
            )
            .ToArray();
        _queue.Clear();

        foreach (var item in current)
        {
            _queue.Enqueue(item.Element, item.Priority);
        }
    }

    private DateTimeOffset GetDueAt(SchedulerEntry entry, DateTimeOffset now)
        => entry.State switch
        {
            NpcBrainStateType.Deactivating => now,
            NpcBrainStateType.FaultBackoff => entry.FaultUntil ?? now,
            NpcBrainStateType.Active when entry.Mailbox.HasEvents => now,
            _ => entry.NextThinkAt
        };

    private void RecordDeferred(DateTimeOffset now, IReadOnlySet<Serial> woken)
    {
        var dueMobileIds = _queue.UnorderedItems
            .Where(item => item.Priority.DueTicks <= DueTicks(now))
            .Select(item => item.Element)
            .Where(element =>
                _entries.TryGetValue(element.MobileId, out var entry) &&
                entry.QueueVersion == element.Version &&
                entry.ScheduledDueTicks is not null &&
                entry.State != NpcBrainStateType.Sleeping &&
                GetDueAt(entry, now) <= now
            )
            .Select(element => element.MobileId)
            .Where(mobileId => !woken.Contains(mobileId))
            .Distinct()
            .ToArray();

        foreach (var _ in dueMobileIds)
        {
            _metrics.RecordBrainDeferred();
        }
    }

    private void UpdateGauges()
    {
        _metrics.SetBrainCounts(
            _entries.Values.Count(entry => entry.State == NpcBrainStateType.Active),
            _entries.Values.Count(entry => entry.State == NpcBrainStateType.Sleeping),
            _entries.Values.Count(entry => entry.State == NpcBrainStateType.FaultBackoff)
        );

        var sleepingSectors = _entries.Values
            .Where(entry => entry.State == NpcBrainStateType.Sleeping)
            .Select(entry => _mobiles.GetById(entry.MobileId))
            .Where(mobile => mobile is not null)
            .Select(mobile => (
                mobile!.MapId,
                SectorX: mobile.Position.X >> SectorShift,
                SectorY: mobile.Position.Y >> SectorShift
            ))
            .Distinct()
            .Count();
        _metrics.SetSleepingSectorCount(sleepingSectors);
    }

    private bool IsSectorActive(MobileEntity mobile)
        => _sectors.IsActive(
            mobile.MapId,
            mobile.Position.X >> SectorShift,
            mobile.Position.Y >> SectorShift
        );

    private void PruneFaultLogs(DateTimeOffset now)
    {
        var interval = TimeSpan.FromSeconds(FaultLogIntervalSeconds);

        foreach (var key in _faultLogs
                     .Where(entry => now - entry.Value >= interval)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _faultLogs.Remove(key);
        }
    }

    private static bool HasCurrentDescriptor(SchedulerEntry entry)
        => entry.Descriptor is { } descriptor &&
           string.Equals(descriptor.BrainId, entry.BrainId, StringComparison.Ordinal);

    private static bool TryCacheDescriptor(SchedulerEntry entry, BrainDescriptor? descriptor)
    {
        if (
            descriptor is null ||
            !string.Equals(descriptor.BrainId, entry.BrainId, StringComparison.Ordinal)
        )
        {
            return false;
        }

        entry.Descriptor = descriptor;
        return true;
    }

    private static long DueTicks(DateTimeOffset dueAt)
        => dueAt.UtcDateTime.Ticks;

    private static TimeSpan FailureBackoff(int consecutiveFailures)
        => TimeSpan.FromSeconds(
            consecutiveFailures switch
            {
                1 => 1,
                2 => 5,
                3 => 30,
                _ => 60
            }
        );

    private sealed class SchedulerEntry
    {
        public Serial MobileId { get; }

        public string BrainId { get; set; }

        public BrainDescriptor? Descriptor { get; set; }

        public NpcBrainStateType State { get; set; }

        public int HomeMapId { get; set; }

        public Point3D HomePosition { get; set; }

        public DateTimeOffset NextThinkAt { get; set; }

        public DateTimeOffset? FaultUntil { get; set; }

        public int ConsecutiveFailures { get; set; }

        public long QueueVersion { get; set; }

        public long? ScheduledDueTicks { get; set; }

        public NpcBrainMailbox Mailbox { get; }

        public SchedulerEntry(
            Serial mobileId,
            string brainId,
            NpcBrainStateType state,
            int homeMapId,
            Point3D homePosition,
            DateTimeOffset nextThinkAt,
            NpcBrainMailbox mailbox
        )
        {
            MobileId = mobileId;
            BrainId = brainId;
            State = state;
            HomeMapId = homeMapId;
            HomePosition = homePosition;
            NextThinkAt = nextThinkAt;
            Mailbox = mailbox;
        }
    }
}
