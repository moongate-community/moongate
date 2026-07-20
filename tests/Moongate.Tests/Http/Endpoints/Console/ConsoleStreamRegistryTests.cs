using Moongate.Http.Plugin.Data.Console;
using Moongate.Http.Plugin.Services.Console;
using Xunit;

namespace Moongate.Tests.Http.Endpoints.Console;

public class ConsoleStreamRegistryTests
{
    [Fact]
    public void Open_returns_a_unique_id_and_a_live_writer()
    {
        var registry = new ConsoleStreamRegistry();

        var (id1, _) = registry.Open();
        var (id2, _) = registry.Open();

        Assert.NotEqual(id1, id2);
        Assert.True(registry.TryGetWriter(id1, out _));
    }

    [Fact]
    public async Task Written_events_reach_the_reader()
    {
        var registry = new ConsoleStreamRegistry();
        var (id, reader) = registry.Open();
        Assert.True(registry.TryGetWriter(id, out var writer));

        writer.TryWrite(new ConsoleStreamEvent("line", "hello"));
        registry.Close(id);

        var events = new List<ConsoleStreamEvent>();
        await foreach (var evt in reader.ReadAllAsync())
        {
            events.Add(evt);
        }

        Assert.Equal(new ConsoleStreamEvent("line", "hello"), Assert.Single(events));
    }

    [Fact]
    public void Close_makes_the_id_unresolvable()
    {
        var registry = new ConsoleStreamRegistry();
        var (id, _) = registry.Open();

        registry.Close(id);

        Assert.False(registry.TryGetWriter(id, out _));
    }
}
