using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Gumps;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Drops a session's open gumps when it goes.
/// <para>
/// The service keeps what it drew for each session so a response can be checked against it, and
/// nothing else ever removes that entry: a gump is forgotten when it answers, but a player who
/// closes the client with one open never answers. Without this the map grows for the life of the
/// process, and a reused session id would inherit a stranger's open gumps — including the button ids
/// that pass validation.
/// </para>
/// </summary>
public sealed class GumpSubscriber : IEventSubscriberRegistration
{
    private readonly IGumpService _gumps;

    public GumpSubscriber(IGumpService gumps)
    {
        _gumps = gumps;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<SessionDestroyedEvent>(OnSessionDestroyed);

    public Task OnSessionDestroyed(SessionDestroyedEvent @event, CancellationToken cancellationToken)
    {
        _gumps.CloseAll(@event.Session);

        return Task.CompletedTask;
    }
}
