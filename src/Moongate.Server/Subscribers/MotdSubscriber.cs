using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>Greets a player entering the world with the message of the day.</summary>
public sealed class MotdSubscriber : IEventSubscriberRegistration
{
    private readonly IMotdService _motd;
    private readonly ISessionManager _sessions;
    private readonly IChatService _chat;

    public MotdSubscriber(IMotdService motd, ISessionManager sessions, IChatService chat)
    {
        _motd = motd;
        _sessions = sessions;
        _chat = chat;
    }

    public Task OnPlayerEnteredWorld(PlayerEnteredWorldEvent message, CancellationToken cancellationToken)
    {
        // The event carries a session id rather than the session: a client that dropped during the
        // enter-world burst is gone by now, and has nobody to greet.
        if (!_sessions.TryGet(message.SessionId, out var session))
        {
            return Task.CompletedTask;
        }

        foreach (var line in _motd.Lines())
        {
            _chat.SendSystemMessage(session, line);
        }

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<PlayerEnteredWorldEvent>(OnPlayerEnteredWorld);
}
