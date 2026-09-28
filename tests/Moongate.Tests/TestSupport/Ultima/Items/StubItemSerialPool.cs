using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Hands out the serials a test queued, and none once they run out.
/// </summary>
public sealed class StubItemSerialPool : IItemSerialPool
{
    public Queue<Serial> Serials { get; } = new();

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public bool TryTake(out Serial serial)
    {
        return Serials.TryDequeue(out serial);
    }
}
