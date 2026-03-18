using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Mutable runtime state for one NPC Lua brain instance.
/// </summary>
public sealed class LuaBrainRuntimeState
{
    public LuaBrainRuntimeState(UOMobileEntity mobile, string brainId, string brainTableName)
    {
        Mobile = mobile;
        BrainId = brainId;
        BrainTableName = brainTableName;
    }

    public UOMobileEntity Mobile { get; set; }

    public string BrainId { get; set; }

    public string BrainTableName { get; set; }

    public long AiNextWakeTime { get; set; }

    public bool IsFaulted { get; set; }

    public Coroutine? BrainCoroutine { get; set; }

    public DynValue? OnSpeechFunction { get; set; }

    public DynValue? OnEventFunction { get; set; }

    public DynValue? OnDeathFunction { get; set; }

    public DynValue? OnSpawnFunction { get; set; }

    public DynValue? OnAttackFunction { get; set; }

    public DynValue? OnMissedAttackFunction { get; set; }

    public DynValue? OnAttackedFunction { get; set; }

    public DynValue? OnMissedByAttackFunction { get; set; }

    public DynValue? OnInRangeFunction { get; set; }

    public DynValue? OnOutRangeFunction { get; set; }

    public DynValue? OnGetContextMenusFunction { get; set; }

    public DynValue? OnSelectedContextMenuFunction { get; set; }

    public Serial MobileId => Mobile.Id;

    public Queue<SpeechHeardEvent> PendingSpeech { get; } = new();

    public Queue<LuaBrainDeathContext> PendingDeath { get; } = new();

    public Queue<MobileSpawnedFromSpawnerEvent> PendingSpawn { get; } = new();

    public Queue<LuaBrainCombatHookContext> PendingCombatHooks { get; } = new();

    public Queue<LuaBrainInRangeContext> PendingInRange { get; } = new();

    public Queue<LuaBrainInRangeContext> PendingOutRange { get; } = new();
}
