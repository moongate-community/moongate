using Moongate.Server.Interfaces.Loading;
using Moongate.Server.Services.Loading;

namespace Moongate.Tests.Server;

public class DataLoaderServiceTests
{
    private sealed class RecordingLoader : IDataLoader
    {
        private readonly string _name;
        private readonly List<string> _log;

        public RecordingLoader(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public ValueTask LoadAsync(CancellationToken ct = default)
        {
            _log.Add(_name);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingLoader : IDataLoader
    {
        public ValueTask LoadAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task ExecuteLoadersAsync_RunsLoadersInListOrder()
    {
        var log = new List<string>();
        var service = new DataLoaderService([
            new RecordingLoader("a", log),
            new RecordingLoader("b", log),
            new RecordingLoader("c", log)
        ]);

        await service.ExecuteLoadersAsync();

        Assert.Equal(new[] { "a", "b", "c" }, log);
    }

    [Fact]
    public async Task ExecuteLoadersAsync_EmptyList_IsNoOp()
    {
        var service = new DataLoaderService([]);

        await service.ExecuteLoadersAsync();
    }

    [Fact]
    public async Task ExecuteLoadersAsync_PropagatesLoaderException()
    {
        var service = new DataLoaderService([new ThrowingLoader()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteLoadersAsync().AsTask());
    }

    [Fact]
    public async Task StartAsync_ExecutesLoaders()
    {
        var log = new List<string>();
        var service = new DataLoaderService([new RecordingLoader("only", log)]);

        await service.StartAsync();

        Assert.Equal(new[] { "only" }, log);
    }
}
