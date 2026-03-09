using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Modules;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Modules;

public sealed class SpawnModuleTests
{
    private sealed class SpawnModuleTestSpawnService : ISpawnService
    {
        public bool TriggerResult { get; set; }
        public Serial LastTriggerItemId { get; private set; }

        public int GetTrackedSpawnerCount()
            => 0;

        public Task<bool> TriggerAsync(Serial spawnerItemId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastTriggerItemId = spawnerItemId;

            return Task.FromResult(TriggerResult);
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public void Activate_WhenItemSerialIsZero_ShouldReturnFalse()
    {
        var service = new SpawnModuleTestSpawnService();
        var module = new SpawnModule(service);

        var result = module.Activate(0);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Activate_WhenServiceReturnsTrue_ShouldReturnTrue()
    {
        var service = new SpawnModuleTestSpawnService
        {
            TriggerResult = true
        };
        var module = new SpawnModule(service);

        var result = module.Activate(0x40000001);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.LastTriggerItemId, Is.EqualTo((Serial)0x40000001u));
            }
        );
    }
}
