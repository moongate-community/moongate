using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Schedule;

public sealed class RecordingBroadcastService : IBroadcastService
{
    public List<string> Sent { get; } = [];

    public bool Fail { get; set; }

    public Task<int> BroadcastAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("The broadcast failed.");
        }

        Sent.Add(text);

        return Task.FromResult(1);
    }
}
