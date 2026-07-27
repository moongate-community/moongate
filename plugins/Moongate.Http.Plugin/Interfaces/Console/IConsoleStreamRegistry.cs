using System.Threading.Channels;
using Moongate.Http.Plugin.Data.Console;

namespace Moongate.Http.Plugin.Interfaces.Console;

/// <summary>Holds the open console SSE connections, keyed by an unguessable connection id.</summary>
public interface IConsoleStreamRegistry
{
    /// <summary>Opens a new connection: returns its id and the reader the SSE endpoint streams from.</summary>
    (string ConnectionId, ChannelReader<ConsoleStreamEvent> Reader) Open();

    /// <summary>Gets the writer for an open connection; false if the id is unknown or closed.</summary>
    bool TryGetWriter(string connectionId, out ChannelWriter<ConsoleStreamEvent> writer);

    /// <summary>Completes and removes a connection; safe to call more than once.</summary>
    void Close(string connectionId);
}
