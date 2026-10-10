using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     One repeating <c>npc_think</c> timer per awake NPC. The first think comes after a random 1–256 ms (the wheel
///     refuses a zero delay), as ModernUO's
///     AITimer, so NPCs woken together do not think in the same tick.
/// </summary>
public sealed class NpcTickService : INpcTickService
{
    public const string TimerName = "npc_think";

    private const int MaxStartJitterMs = 256;
    private readonly ILogger _logger = Log.ForContext<NpcTickService>();
    private readonly ITimerService _timers;
    private readonly NpcsConfig _config;

    private readonly INpcThinker? _thinker;

    // Lazy: the regeneration needs the mobiles, which need the sectors, which need this service.
    private readonly Lazy<IRegenerationService>? _regeneration;
    private readonly Lazy<IParalysisService>? _paralysis;
    private readonly Lazy<IDisguiseService>? _disguise;
    private readonly Dictionary<Serial, string> _awake = [];

    public int AwakeCount => _awake.Count;

    public long ThinkCount { get; private set; }

    public NpcTickService(
        ITimerService timers,
        NpcsConfig config,
        INpcThinker? thinker = null,
        Lazy<IRegenerationService>? regeneration = null,
        Lazy<IParalysisService>? paralysis = null,
        Lazy<IDisguiseService>? disguise = null
    )
    {
        _paralysis = paralysis;
        _disguise = disguise;
        _regeneration = regeneration;
        _timers = timers;
        _config = config;
        _thinker = thinker;
    }

    public bool IsAwake(Serial serial)
    {
        return _awake.ContainsKey(serial);
    }

    public void Wake(MobileEntity npc)
    {
        if (!npc.IsNpc || _awake.ContainsKey(npc.Id))
        {
            return;
        }

        // The sectors call Wake in the middle of their bookkeeping: a wheel that refuses (closed or full) must not
        // throw into them, so the NPC just stays asleep.
        try
        {
            _awake[npc.Id] = _timers.RegisterTimer(
                TimerName,
                TimeSpan.FromMilliseconds(_config.ThinkIntervalMs),
                () => Think(npc),
                TimeSpan.FromMilliseconds(Random.Shared.Next(1, MaxStartJitterMs + 1)),
                true
            );
        }
        catch (InvalidOperationException exception)
        {
            _logger.Warning(exception, "NPC {Serial} ({Template}) could not get a think timer", npc.Id, npc.TemplateId);
        }
    }

    public void Sleep(MobileEntity npc)
    {
        if (_awake.Remove(npc.Id, out var timerId))
        {
            _timers.UnregisterTimer(timerId);
        }
    }

    // A timer callback that throws closes the timer wheel: one broken NPC must not stop the server.
    private void Think(MobileEntity npc)
    {
        ThinkCount++;

        try
        {
            // An NPC regenerates while it thinks: asleep, far from every player, it does not.
            _regeneration?.Value.Tick(npc);
            // A paralysis or a disguise it was saved with has no timer after a restart: it goes on for what was left of
            // it, or ends. Nothing happens for an NPC that has neither.
            _paralysis?.Value.Resume(npc);
            _disguise?.Value.Resume(npc);
            _thinker?.Think(npc);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "NPC {Serial} ({Template}) failed to think", npc.Id, npc.TemplateId);
        }
    }
}
