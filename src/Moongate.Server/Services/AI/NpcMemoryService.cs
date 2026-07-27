using Moongate.Core.Extensions;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.AI;

/// <summary>Durable per-NPC memory backed by the npc_memory store, resolved from an ambient brain context.</summary>
public sealed class NpcMemoryService : INpcMemoryService
{
    private const int MaxKeys = 128;
    private const int MaxKeyLength = 64;
    private const int MaxValueLength = 256;

    private static readonly IReadOnlyDictionary<string, NpcMemoryValue> Empty =
        new Dictionary<string, NpcMemoryValue>();

    private readonly IEntityStore<NpcMemoryEntity, Serial> _memory;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly ILoopAffinity _loopAffinity;

    private BrainContext? _current;

    public NpcMemoryService(IPersistenceService persistenceService, ILoopAffinity loopAffinity)
    {
        _memory = persistenceService.GetStore<NpcMemoryEntity, Serial>();
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _loopAffinity = loopAffinity;
    }

    public IDisposable Begin(BrainContext context)
    {
        _loopAffinity.AssertOnLoop("memory.begin");
        var previous = _current;
        _current = context;
        return new ContextScope(this, previous);
    }

    public bool Set(string key, NpcMemoryValue value)
    {
        _loopAffinity.AssertOnLoop("memory.set");
        var mobileId = Current();

        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
        {
            return false;
        }

        if (value.Type == MemoryValueType.String && (value.Text?.Length ?? 0) > MaxValueLength)
        {
            return false;
        }

        if (_mobiles.GetById(mobileId) is null)
        {
            return false;
        }

        var entity = _memory.GetById(mobileId) ?? new NpcMemoryEntity { Id = mobileId };

        if (!entity.Memory.ContainsKey(key) && entity.Memory.Count >= MaxKeys)
        {
            return false;
        }

        entity.Memory[key] = value;
        _memory.UpsertAsync(entity).WaitSync();

        return true;
    }

    public NpcMemoryValue? Get(string key)
    {
        var mobileId = Current();

        return _memory.GetById(mobileId) is { } entity && entity.Memory.TryGetValue(key, out var value)
            ? value
            : null;
    }

    public bool Delete(string key)
    {
        _loopAffinity.AssertOnLoop("memory.delete");
        var mobileId = Current();

        if (_memory.GetById(mobileId) is not { } entity || !entity.Memory.Remove(key))
        {
            return false;
        }

        _memory.UpsertAsync(entity).WaitSync();

        return true;
    }

    public IReadOnlyDictionary<string, NpcMemoryValue> All()
    {
        var mobileId = Current();

        return _memory.GetById(mobileId)?.Memory ?? Empty;
    }

    public void Forget(Serial mobileId)
    {
        _loopAffinity.AssertOnLoop("memory.forget");

        if (_memory.GetById(mobileId) is not null)
        {
            _memory.RemoveAsync(mobileId).WaitSync();
        }
    }

    private Serial Current()
        => (_current ?? throw new InvalidOperationException("memory.* is only callable inside an NPC brain tick.")).Self.Id;

    private sealed class ContextScope : IDisposable
    {
        private readonly NpcMemoryService _owner;
        private readonly BrainContext? _previous;

        public ContextScope(NpcMemoryService owner, BrainContext? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        public void Dispose()
        {
            _owner._current = _previous;
        }
    }
}
