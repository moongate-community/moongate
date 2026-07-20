using System.Net.ServerSentEvents;
using Moongate.Http.Plugin.Data.Console;
using Moongate.Http.Plugin.Services.Console;
using Xunit;

namespace Moongate.Tests.Http.Endpoints.Console;

public class ConsoleSseStreamTests
{
    [Fact]
    public async Task From_yields_ready_then_each_channel_event()
    {
        var registry = new ConsoleStreamRegistry();
        var (id, reader) = registry.Open();
        Assert.True(registry.TryGetWriter(id, out var writer));

        writer.TryWrite(new ConsoleStreamEvent("line", "hello"));
        writer.TryWrite(new ConsoleStreamEvent("done", "broadcast hello"));
        registry.Close(id); // completes the channel so the enumeration ends

        var items = new List<SseItem<string>>();
        await foreach (var item in ConsoleSseStream.From(id, reader, CancellationToken.None))
        {
            items.Add(item);
        }

        Assert.Equal(("ready", id), (items[0].EventType, items[0].Data));
        Assert.Equal(("line", "hello"), (items[1].EventType, items[1].Data));
        Assert.Equal(("done", "broadcast hello"), (items[2].EventType, items[2].Data));
    }
}
