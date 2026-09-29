using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     One repeating <c>npc_think</c> timer per awake NPC. The first think comes after a random 0–255 ms, as ModernUO's
///     AITimer, so NPCs woken together do not think in the same tick.
/// </summary>
public sealed class NpcTickService : INpcTickService
{
    public const string TimerName = "npc_think";

    private readonly ILogger _logger = Log.ForContext<NpcTickService>();
    private readonly ITimerService _timers;
    private readonly NpcsConfig _config;
    private readonly INpcThinker? _thinker;
    private readonly Dictionary<Serial, string> _awake = [];

    public int AwakeCount => _awake.Count;

    public long ThinkCount { get; private set; }

    public NpcTickService(ITimerService timers, NpcsConfig config, INpcThinker? thinker = null)
    {
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

        _awake[npc.Id] = _timers.RegisterTimer(
            TimerName,
            TimeSpan.FromMilliseconds(_config.ThinkIntervalMs),
            () => Think(npc),
            TimeSpan.FromMilliseconds(Random.Shared.Next(256)),
            true
        );
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
            _thinker?.Think(npc);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "NPC {Serial} ({Template}) failed to think", npc.Id, npc.TemplateId);
        }
    }
}
