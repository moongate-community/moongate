using Moongate.Server.Data.Events;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Interfaces.Services.Events;

namespace Moongate.Tests.Server.Support;

public sealed class TestBroadcastFromServerEventListener : IGameEventListener<BroadcastFromServerEvent>
{
    public int EventCount { get; private set; }

    public BroadcastFromServerEvent LastEvent { get; private set; }

    public Task HandleAsync(BroadcastFromServerEvent gameEvent, CancellationToken cancellationToken = default)
    {
        EventCount++;
        LastEvent = gameEvent;

        return Task.CompletedTask;
    }
}
