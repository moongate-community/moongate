using System.Threading.Channels;
using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Services.GameLoop.Internal;

/// <summary>Runs synchronous handlers on its caller's thread; the service owns failure policy.</summary>
internal sealed class GameLoopPump
{
    private readonly ChannelReader<IGameLoopWorkItem> _reader;
    private readonly int _maxWorkItemsPerBatch;

    internal GameLoopPump(ChannelReader<IGameLoopWorkItem> reader, int maxWorkItemsPerBatch)
    {
        _reader = reader;
        _maxWorkItemsPerBatch = maxWorkItemsPerBatch;
    }

    internal int RunBatch()
    {
        var attempted = 0;
        // Check the budget before dequeue so the next batch retains every queued item.
        while (attempted < _maxWorkItemsPerBatch && _reader.TryRead(out var workItem))
        {
            attempted++;
            workItem.Execute();
        }

        return attempted;
    }
}
