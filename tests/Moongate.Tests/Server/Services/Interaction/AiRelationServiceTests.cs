using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Factions;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class AiRelationServiceTests
{
    [Test]
    public void Compute_WhenViewerTargetsSelf_ShouldReturnFriendly()
    {
        var service = new AiRelationService();
        var mobile = CreateGuard((Serial)0x0100u);

        var result = service.Compute(mobile, mobile);

        Assert.That(result, Is.EqualTo(AiRelation.Friendly));
    }

    [Test]
    public void Compute_WhenGuardSeesInnocentPlayer_ShouldReturnNeutral()
    {
        var service = new AiRelationService();
        var viewer = CreateGuard((Serial)0x0100u);
        var player = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);

        var result = service.Compute(viewer, player);

        Assert.That(result, Is.EqualTo(AiRelation.Neutral));
    }

    [Test]
    public void Compute_WhenGuardSeesCriminalPlayer_ShouldReturnHostile()
    {
        var service = new AiRelationService();
        var viewer = CreateGuard((Serial)0x0100u);
        var player = CreatePlayer((Serial)0x0101u, Notoriety.Criminal);

        var result = service.Compute(viewer, player);

        Assert.That(result, Is.EqualTo(AiRelation.Hostile));
    }

    [Test]
    public void Compute_WhenGuardSeesMonsterNpc_ShouldReturnHostile()
    {
        var service = new AiRelationService();
        var viewer = CreateGuard((Serial)0x0100u);
        var zombie = CreateMonster((Serial)0x0102u);

        var result = service.Compute(viewer, zombie);

        Assert.That(result, Is.EqualTo(AiRelation.Hostile));
    }

    [Test]
    public void Compute_WhenRecentAggressorExists_ShouldReturnHostile()
    {
        var service = new AiRelationService();
        var viewer = CreateGuard((Serial)0x0100u);
        var player = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);
        var nowUtc = DateTime.UtcNow;

        viewer.RefreshAggressor(player.Id, viewer.Id, nowUtc);

        var result = service.Compute(viewer, player);

        Assert.That(result, Is.EqualTo(AiRelation.Hostile));
    }

    [Test]
    public void Compute_WhenViewerAndTargetShareFaction_ShouldReturnFriendly()
    {
        var service = new AiRelationService(CreateFactionTemplateService());
        var viewer = CreateGuard((Serial)0x0100u);
        var target = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);
        viewer.FactionId = "true_britannians";
        target.FactionId = "true_britannians";

        var result = service.Compute(viewer, target);

        Assert.That(result, Is.EqualTo(AiRelation.Friendly));
    }

    [Test]
    public void Compute_WhenViewerAndTargetBelongToEnemyFactions_ShouldReturnHostile()
    {
        var service = new AiRelationService(CreateFactionTemplateService());
        var viewer = CreateGuard((Serial)0x0100u);
        var target = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);
        viewer.FactionId = "true_britannians";
        target.FactionId = "shadowlords";

        var result = service.Compute(viewer, target);

        Assert.That(result, Is.EqualTo(AiRelation.Hostile));
    }

    private static UOMobileEntity CreateGuard(Serial id)
        => new()
        {
            Id = id,
            IsPlayer = false,
            Notoriety = Notoriety.Innocent,
            BaseBody = 0x0190
        };

    private static UOMobileEntity CreatePlayer(Serial id, Notoriety notoriety)
        => new()
        {
            Id = id,
            IsPlayer = true,
            Notoriety = notoriety,
            BaseBody = 0x0190
        };

    private static UOMobileEntity CreateMonster(Serial id)
        => new()
        {
            Id = id,
            IsPlayer = false,
            Notoriety = Notoriety.CanBeAttacked,
            BaseBody = 0x0003
        };

    private static FactionTemplateService CreateFactionTemplateService()
    {
        var service = new FactionTemplateService();
        service.Upsert(
            new FactionDefinition
            {
                Id = "true_britannians",
                Name = "True Britannians",
                EnemyFactionIds = ["shadowlords"]
            }
        );
        service.Upsert(
            new FactionDefinition
            {
                Id = "shadowlords",
                Name = "Shadowlords",
                EnemyFactionIds = ["true_britannians"]
            }
        );

        return service;
    }
}
