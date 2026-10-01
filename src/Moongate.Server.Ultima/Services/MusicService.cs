using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Internal.Music;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The region music, as ModernUO plays it: the track of the region a player enters when it differs from the one
///     playing; the map's track where the region has none (<c>music</c> in <c>maps.toml</c>); silence where neither has
///     any.
/// </summary>
public sealed class MusicService : IMusicService
{
    private readonly ConcurrentDictionary<Serial, MusicListener> _listeners = new();
    private readonly IDataLoaderService _data;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly IMoongateEventBus _events;
    private readonly IGameLoopService _loop;

    private IDisposable? _logins;

    public MusicService(
        IDataLoaderService data,
        ISessionService sessions,
        IPacketSendService sender,
        IMoongateEventBus events,
        IGameLoopService loop
    )
    {
        _data = data;
        _sessions = sessions;
        _sender = sender;
        _events = events;
        _loop = loop;
    }

    public Task StartAsync()
    {
        // The event comes from the login handler's thread; the listeners change on the game loop only.
        _logins = _events.Subscribe<CharacterEnteredWorldEvent>(
            async (evt, cancellationToken) =>
            {
                var work = new LoopActionWorkItem(() => LoggedIn(evt.Character));
                await _loop.PostAsync(work, cancellationToken);
                await work.Completion;
            }
        );

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _logins?.Dispose();
        _logins = null;

        return Task.CompletedTask;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        var listener = _listeners.GetOrAdd(player.Id, _ => new() { Player = player });

        // A relogin brings a new object for the same character: follow the new one.
        if (!ReferenceEquals(listener.Player, player))
        {
            listener = _listeners[player.Id] = new() { Player = player };
        }

        listener.Region = current;
        Send(listener, MusicOf(listener));
    }

    public void Left(Serial player)
    {
        _listeners.TryRemove(player, out _);
    }

    public MusicType MusicOf(MobileEntity player)
    {
        return _listeners.TryGetValue(player.Id, out var listener) && ReferenceEquals(listener.Player, player)
            ? MusicOf(listener)
            : MapMusic(player.Map);
    }

    public void Play(MobileEntity player, MusicType music)
    {
        if (_listeners.TryGetValue(player.Id, out var listener) && ReferenceEquals(listener.Player, player))
        {
            Send(listener, music);
        }
    }

    private void LoggedIn(MobileEntity character)
    {
        // A player that left before its login completed is not followed any more.
        if (!_sessions.TryGetByCharacterId(character.Id, out var session) ||
            !_listeners.TryGetValue(character.Id, out var listener) ||
            !ReferenceEquals(listener.Player, character))
        {
            return;
        }

        listener.SessionId = session.SessionId;
        listener.LastSent = null;
        Send(listener, MusicOf(listener));
    }

    private MusicType MusicOf(MusicListener listener)
    {
        return listener.Region?.Music ?? MapMusic(listener.Player.Map);
    }

    private MusicType MapMusic(MapType map)
    {
        return _data.GetEntities<MapContent>().FirstOrDefault(content => content.Map == map)?.Music ?? MusicType.NoMusic;
    }

    private void Send(MusicListener listener, MusicType music)
    {
        if (listener.SessionId is not { } sessionId || listener.LastSent == music)
        {
            return;
        }

        if (_sender.TrySend(sessionId, new PlayMusicPacket(music)))
        {
            listener.LastSent = music;
        }
    }
}
