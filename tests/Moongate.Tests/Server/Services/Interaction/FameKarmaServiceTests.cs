using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class FameKarmaServiceTests
{
    [Test]
    public async Task AwardNpcKillAsync_WhenVictimIsNpcAndKillerIsPlayer_ShouldAwardAndPersist()
    {
        var mobileService = new TestMobileService();
        var service = new FameKarmaService(mobileService);
        var victim = CreateMobile(isPlayer: false, fame: 250, karma: -250);
        var killer = CreateMobile(isPlayer: true, fame: 10, karma: 20);

        await service.AwardNpcKillAsync(victim, killer);

        Assert.That(killer.Fame, Is.EqualTo(12));
        Assert.That(killer.Karma, Is.EqualTo(18));
        Assert.That(mobileService.UpdatedMobiles, Has.Count.EqualTo(1));
        Assert.That(mobileService.UpdatedMobiles[0], Is.SameAs(killer));
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public async Task AwardNpcKillAsync_WhenAwardGateFails_ShouldNotPersist(bool victimIsPlayer, bool killerIsPlayer)
    {
        var mobileService = new TestMobileService();
        var service = new FameKarmaService(mobileService);
        var victim = CreateMobile(isPlayer: victimIsPlayer, fame: 500, karma: 500);
        var killer = CreateMobile(isPlayer: killerIsPlayer, fame: 1, karma: 1);

        await service.AwardNpcKillAsync(victim, killer);

        Assert.That(mobileService.UpdatedMobiles, Is.Empty);
        Assert.That(killer.Fame, Is.EqualTo(1));
        Assert.That(killer.Karma, Is.EqualTo(1));
    }

    [Test]
    public async Task AwardNpcKillAsync_WhenAwardsAreZero_ShouldNotPersist()
    {
        var mobileService = new TestMobileService();
        var service = new FameKarmaService(mobileService);
        var victim = CreateMobile(isPlayer: false, fame: 99, karma: 99);
        var killer = CreateMobile(isPlayer: true, fame: 7, karma: 8);

        await service.AwardNpcKillAsync(victim, killer);

        Assert.That(mobileService.UpdatedMobiles, Is.Empty);
        Assert.That(killer.Fame, Is.EqualTo(7));
        Assert.That(killer.Karma, Is.EqualTo(8));
    }

    [Test]
    public async Task AwardNpcKillAsync_WhenOnlyFameAwards_ShouldPersistUpdatedKiller()
    {
        var mobileService = new TestMobileService();
        var service = new FameKarmaService(mobileService);
        var victim = CreateMobile(isPlayer: false, fame: 500, karma: 99);
        var killer = CreateMobile(isPlayer: true, fame: 11, karma: 22);

        await service.AwardNpcKillAsync(victim, killer);

        Assert.That(killer.Fame, Is.EqualTo(16));
        Assert.That(killer.Karma, Is.EqualTo(22));
        Assert.That(mobileService.UpdatedMobiles, Has.Count.EqualTo(1));
    }

    private static UOMobileEntity CreateMobile(bool isPlayer, int fame, int karma)
        => new()
        {
            Id = (Serial)(isPlayer ? 0x40000001u : 0x00001001u),
            IsPlayer = isPlayer,
            Fame = fame,
            Karma = karma,
            Location = new Point3D(0, 0, 0)
        };

    private sealed class TestMobileService : IMobileService
    {
        public List<UOMobileEntity> UpdatedMobiles { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            UpdatedMobiles.Add(mobile);

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;

            return Task.FromResult(false);
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;

            return Task.FromResult<UOMobileEntity?>(null);
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;
            _ = cancellationToken;

            return Task.FromResult(new List<UOMobileEntity>());
        }

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
        {
            _ = templateId;
            _ = location;
            _ = mapId;
            _ = accountId;
            _ = cancellationToken;

            return Task.FromException<UOMobileEntity>(new NotSupportedException());
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
        {
            _ = templateId;
            _ = location;
            _ = mapId;
            _ = accountId;
            _ = cancellationToken;

            return Task.FromResult((false, (UOMobileEntity?)null));
        }
    }
}
