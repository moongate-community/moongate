using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Commands;

public sealed class ControlledBroadcastService : IBroadcastService
{
    public Task<int> Delivery { get; set; } = Task.FromResult(0);
    public List<string> Messages { get; } = [];

    public Task<int> BroadcastAsync(string text, CancellationToken cancellationToken = default)
    {
        Messages.Add(text);

        return Delivery.WaitAsync(cancellationToken);
    }
}
