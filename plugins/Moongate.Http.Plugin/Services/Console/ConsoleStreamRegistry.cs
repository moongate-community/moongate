using System.Collections.Concurrent;
using System.Threading.Channels;
using Moongate.Http.Plugin.Data.Console;
using Moongate.Http.Plugin.Interfaces.Console;

namespace Moongate.Http.Plugin.Services.Console;

/// <summary>
/// In-memory registry of open console SSE connections. Each connection is an unbounded channel; the POST
/// handler writes reply events to it and the SSE endpoint drains it. Thread-safe: writes come from the
/// game loop, reads from the ASP.NET request thread.
/// </summary>
public sealed class ConsoleStreamRegistry : IConsoleStreamRegistry
{
    private readonly ConcurrentDictionary<string, Channel<ConsoleStreamEvent>> _channels = new();

    public (string ConnectionId, ChannelReader<ConsoleStreamEvent> Reader) Open()
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateUnbounded<ConsoleStreamEvent>();
        _channels[connectionId] = channel;

        return (connectionId, channel.Reader);
    }

    public bool TryGetWriter(string connectionId, out ChannelWriter<ConsoleStreamEvent> writer)
    {
        if (_channels.TryGetValue(connectionId, out var channel))
        {
            writer = channel.Writer;

            return true;
        }

        writer = Channel.CreateUnbounded<ConsoleStreamEvent>().Writer;

        return false;
    }

    public void Close(string connectionId)
    {
        if (_channels.TryRemove(connectionId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}
