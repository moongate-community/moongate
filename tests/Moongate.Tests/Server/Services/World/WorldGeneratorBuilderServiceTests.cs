using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;

namespace Moongate.Tests.Server.Services.World;

public class WorldGeneratorBuilderServiceTests
{
    private sealed class RecordingWorldGenerator : IWorldGenerator
    {
        private readonly List<string> _executionOrder;
        public string Name { get; }

        public RecordingWorldGenerator(string name, List<string> executionOrder)
        {
            Name = name;
            _executionOrder = executionOrder;
        }

        public Task GenerateAsync(Action<string>? logCallback = null, CancellationToken cancellationToken = default)
        {
            _executionOrder.Add(Name);
            logCallback?.Invoke($"Generator {Name} executed.");

            return Task.CompletedTask;
        }
    }

    private sealed class CancellationAwareWorldGenerator : IWorldGenerator
    {
        public string Name => "test";
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task GenerateAsync(Action<string>? logCallback = null, CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;

            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task GenerateAsync_ShouldExecuteAllGeneratorsInOrder()
    {
        var executionOrder = new List<string>();
        var logs = new List<string>();
        var service = new WorldGeneratorBuilderService(
            [
                new RecordingWorldGenerator("first", executionOrder),
                new RecordingWorldGenerator("second", executionOrder)
            ]
        );

        await service.GenerateAsync(logCallback: logs.Add);

        Assert.Multiple(
            () =>
            {
                Assert.That(executionOrder, Is.EqualTo(new[] { "first", "second" }));
                Assert.That(logs.Any(static line => line.Contains("Starting world generator 'first'")), Is.True);
                Assert.That(logs.Any(static line => line.Contains("Completed world generator 'second'")), Is.True);
            }
        );
    }

    [Test]
    public async Task GenerateAsync_ShouldForwardCancellationToken()
    {
        var source = new CancellationTokenSource();
        var generator = new CancellationAwareWorldGenerator();
        var service = new WorldGeneratorBuilderService([generator]);

        await service.GenerateAsync(cancellationToken: source.Token);

        Assert.That(generator.ReceivedCancellationToken, Is.EqualTo(source.Token));
    }

    [Test]
    public void GenerateAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        var generator = new CancellationAwareWorldGenerator();
        var service = new WorldGeneratorBuilderService([generator]);

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await service.GenerateAsync(cancellationToken: source.Token)
        );
    }

    [Test]
    public async Task GenerateAsync_WithGeneratorName_ShouldExecuteOnlyMatchedGenerator()
    {
        var executionOrder = new List<string>();
        var service = new WorldGeneratorBuilderService(
            [
                new RecordingWorldGenerator("items", executionOrder),
                new RecordingWorldGenerator("doors", executionOrder),
                new RecordingWorldGenerator("mobiles", executionOrder)
            ]
        );

        await service.GenerateAsync("doors");

        Assert.That(executionOrder, Is.EqualTo(new[] { "doors" }));
    }

    [Test]
    public void GenerateAsync_WithNoGenerators_ShouldComplete()
    {
        var service = new WorldGeneratorBuilderService(Array.Empty<IWorldGenerator>());

        Assert.DoesNotThrowAsync(async () => await service.GenerateAsync());
    }

    [Test]
    public void GenerateAsync_WithUnknownGeneratorName_ShouldThrowInvalidOperationException()
    {
        var service = new WorldGeneratorBuilderService([new RecordingWorldGenerator("doors", [])]);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.GenerateAsync("unknown"));
    }

    [Test]
    public async Task GenerateAsync_WithWhitespaceGeneratorName_ShouldExecuteAllGenerators()
    {
        var executionOrder = new List<string>();
        var service = new WorldGeneratorBuilderService(
            [
                new RecordingWorldGenerator("items", executionOrder),
                new RecordingWorldGenerator("doors", executionOrder)
            ]
        );

        await service.GenerateAsync(" ");

        Assert.That(executionOrder, Is.EqualTo(new[] { "items", "doors" }));
    }
}
