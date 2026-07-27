using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Moongate.Http.Plugin.Data.Console;

namespace Moongate.Http.Plugin.Services.Console;

/// <summary>
/// Turns a console connection's events into the SSE items its stream emits: a <c>ready</c> item carrying
/// the connection id, then one item per <see cref="ConsoleStreamEvent" /> (its <c>Event</c> becomes the
/// SSE event type, its <c>Text</c> the data). Ends when the channel completes or the token cancels.
/// </summary>
public static class ConsoleSseStream
{
    public static async IAsyncEnumerable<SseItem<string>> From(
        string connectionId,
        ChannelReader<ConsoleStreamEvent> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        yield return new SseItem<string>(connectionId, "ready");

        await foreach (var evt in reader.ReadAllAsync(cancellationToken))
        {
            yield return new SseItem<string>(evt.Text, evt.Event);
        }
    }
}
