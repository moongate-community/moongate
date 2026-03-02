using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;

namespace Moongate.Tests.Server.Services.World;

public class WorldGenerationStartupServiceTests
{
    [Test]
    public async Task StartAsync_ShouldInvokeDoorsGenerator()
    {
        var generatorBuilder = new FakeWorldGeneratorBuilderService();
        var service = new WorldGenerationStartupService(generatorBuilder);

        await service.StartAsync();

        Assert.That(generatorBuilder.LastGeneratorName, Is.EqualTo("doors"));
    }

    private sealed class FakeWorldGeneratorBuilderService : IWorldGeneratorBuilderService
    {
        public string? LastGeneratorName { get; private set; }

        public Task GenerateAsync(
            string? generatorName = null,
            Action<string>? logCallback = null,
            CancellationToken cancellationToken = default
        )
        {
            LastGeneratorName = generatorName;
            logCallback?.Invoke("fake log");

            return Task.CompletedTask;
        }
    }
}
