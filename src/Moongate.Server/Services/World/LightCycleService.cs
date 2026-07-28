using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Core.Interfaces;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using SquidStd.Abstractions.Interfaces.Services;

namespace Moongate.Server.Services.World;

/// <summary>
/// Pushes the light level to each player as it changes. Polls rather than reacting to anything,
/// because the level moves with the player as well as with the clock: walking into a dungeon changes
/// it with no event to hang off. ModernUO polls on the same five-second beat.
/// </summary>
public sealed class LightCycleService : ISquidStdService
{
    private const string TimerName = "light-cycle";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly ISessionManager _sessions;
    private readonly ILightService _light;
    private readonly IGameLoopContext _loop;

    /// <summary>The level each session was last told, so an unchanged one is not resent.</summary>
    private readonly Dictionary<long, int> _lastSent = new();

    public LightCycleService(ISessionManager sessions, ILightService light, IGameLoopContext loop)
    {
        _sessions = sessions;
        _light = light;
        _loop = loop;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _loop.ScheduleRepeating(TimerName, Interval, Tick);

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>Public so a test can drive one beat without waiting on the timer.</summary>
    public void Tick()
    {
        var live = new HashSet<long>();

        foreach (var session in _sessions.All)
        {
            if (session.State != SessionStateType.InWorld || session.Character is not { } character)
            {
                continue;
            }

            live.Add(session.SessionId);

            var level = _light.LevelFor(character.MapId, character.Position);

            if (_lastSent.TryGetValue(session.SessionId, out var last) && last == level)
            {
                continue;
            }

            _lastSent[session.SessionId] = level;
            session.Send(new OverallLightLevelPacket((byte)level));
        }

        // Sessions that have gone away must not keep an entry, or a reused session id would be told
        // nothing until the level next moved.
        foreach (var sessionId in _lastSent.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _lastSent.Remove(sessionId);
        }
    }
}
