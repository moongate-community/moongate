using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Thread-safe storage for lua brain runtime states.
/// </summary>
internal sealed class LuaBrainStateStore
{
    private readonly Dictionary<Serial, LuaBrainRuntimeState> _states = [];
    private readonly Lock _sync = new();

    public void Clear()
    {
        lock (_sync)
        {
            _states.Clear();
        }
    }

    public void EnqueueDeath(Serial mobileId, LuaBrainDeathContext deathContext)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(mobileId, out var state))
            {
                state.PendingDeath.Enqueue(deathContext);
            }
        }
    }

    public void EnqueueCombatHook(Serial mobileId, LuaBrainCombatHookContext combatContext)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(mobileId, out var state))
            {
                state.PendingCombatHooks.Enqueue(combatContext);
            }
        }
    }

    public void EnqueueInRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(listenerNpcId, out var state))
            {
                return;
            }

            state.PendingInRange.Enqueue(
                new(
                    sourceMobile.Id,
                    LuaBrainPayloadFactory.BuildInRangeEventPayload(state.MobileId, sourceMobile, range)
                )
            );
        }
    }

    public void EnqueueOutRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(listenerNpcId, out var state))
            {
                return;
            }

            state.PendingOutRange.Enqueue(
                new(
                    sourceMobile.Id,
                    LuaBrainPayloadFactory.BuildInRangeEventPayload(state.MobileId, sourceMobile, range)
                )
            );
        }
    }

    public void EnqueueSpawn(MobileSpawnedFromSpawnerEvent gameEvent)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(gameEvent.Mobile.Id, out var state))
            {
                state.PendingSpawn.Enqueue(gameEvent);
            }
        }
    }

    public void EnqueueSpeech(SpeechHeardEvent gameEvent)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(gameEvent.ListenerNpcId, out var state))
            {
                state.PendingSpeech.Enqueue(gameEvent);
            }
        }
    }

    public List<LuaBrainRuntimeState> GetAllStates()
    {
        lock (_sync)
        {
            return [.. _states.Values];
        }
    }

    public List<LuaBrainRuntimeState> GetDueStates(long nowMilliseconds)
    {
        lock (_sync)
        {
            return
            [
                .. _states.Values
                          .Where(state => nowMilliseconds >= state.AiNextWakeTime)
                          .Select(static state => state)
            ];
        }
    }

    public void Remove(Serial mobileId)
    {
        lock (_sync)
        {
            _states.Remove(mobileId);
        }
    }

    public bool TryGet(Serial mobileId, out LuaBrainRuntimeState? state)
    {
        lock (_sync)
        {
            return _states.TryGetValue(mobileId, out state);
        }
    }

    public bool TryResolveSourceMobile(Serial mobileId, int mapId, Point3D location, out UOMobileEntity? sourceMobile)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(mobileId, out var trackedState))
            {
                sourceMobile = trackedState.Mobile;

                return true;
            }
        }

        sourceMobile = new()
        {
            Id = mobileId,
            MapId = mapId,
            Location = location,
            IsPlayer = true
        };

        return true;
    }

    public void UpdateTrackedMobilePosition(Serial mobileId, int mapId, Point3D location)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(mobileId, out var tracked))
            {
                tracked.Mobile.MapId = mapId;
                tracked.Mobile.Location = location;
            }
        }
    }

    public void UpdateWakeTime(Serial mobileId, long nextWakeTime)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(mobileId, out var tracked))
            {
                tracked.AiNextWakeTime = nextWakeTime;
            }
        }
    }

    public void Upsert(LuaBrainRuntimeState state)
    {
        lock (_sync)
        {
            _states[state.MobileId] = state;
        }
    }
}
