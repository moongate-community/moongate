using Moongate.News.Plugin.Interfaces;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Events;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.News.Plugin.Subscribers;

/// <summary>
/// Greets a player entering the world with the shard's latest published news.
///
/// One entry, not the archive: a shard that has published twenty things should not fire twenty lines
/// at someone walking through the door. <c>.news</c> is where the rest lives.
/// </summary>
public sealed class NewsMotdSubscriber : IEventSubscriberRegistration
{
    private readonly INewsService _news;
    private readonly ISessionManager _sessions;
    private readonly IChatService _chat;

    public NewsMotdSubscriber(INewsService news, ISessionManager sessions, IChatService chat)
    {
        _news = news;
        _sessions = sessions;
        _chat = chat;
    }

    public Task OnPlayerEnteredWorld(PlayerEnteredWorldEvent message, CancellationToken cancellationToken)
    {
        // Published only. A draft is written and not announced, and sending one would publish it by
        // accident from the one place nobody would think to look.
        if (_news.GetPublished() is not [var latest, ..])
        {
            return Task.CompletedTask;
        }

        // The event carries a session id rather than the session: a client that dropped during the
        // enter-world burst is gone by now, and has nobody to greet.
        if (!_sessions.TryGet(message.SessionId, out var session))
        {
            return Task.CompletedTask;
        }

        _chat.SendSystemMessage(session, latest.Title);

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<PlayerEnteredWorldEvent>(OnPlayerEnteredWorld);
}
