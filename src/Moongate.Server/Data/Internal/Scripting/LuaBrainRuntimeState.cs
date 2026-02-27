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
    private readonly Queue<SpeechHeardEvent> _pendingSpeech = new();

    public LuaBrainRuntimeState(UOMobileEntity mobile, string brainId, string scriptPath)
    {
        Mobile = mobile;
        BrainId = brainId;
        ScriptPath = scriptPath;
    }

    public UOMobileEntity Mobile { get; set; }

    public string BrainId { get; set; }

    public string ScriptPath { get; set; }

    public long AiNextWakeTime { get; set; }

    public bool IsFaulted { get; set; }

    public Coroutine? BrainCoroutine { get; set; }

    public DynValue? OnSpeechFunction { get; set; }

    public Serial MobileId => Mobile.Id;

    public Queue<SpeechHeardEvent> PendingSpeech => _pendingSpeech;
}
