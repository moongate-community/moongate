using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Interfaces.Services.Events;

namespace Moongate.Tests.Server.Support;

public sealed class TestSendMessageFromServerEventListener : IGameEventListener<SendMessageFromServerEvent>
{
    public int EventCount { get; private set; }

    public SendMessageFromServerEvent LastEvent { get; private set; }

    public Task HandleAsync(SendMessageFromServerEvent gameEvent, CancellationToken cancellationToken = default)
    {
        EventCount++;
        LastEvent = gameEvent;

        return Task.CompletedTask;
    }
}
