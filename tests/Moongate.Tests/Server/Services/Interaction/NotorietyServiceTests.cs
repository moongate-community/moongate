using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Factions;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class NotorietyServiceTests
{
    [Test]
    public void Compute_WhenTargetIsInvulnerable_ShouldReturnInvulnerable()
    {
        var service = new NotorietyService();
        var source = CreatePlayer();
        var target = CreateMonster();
        target.IsInvulnerable = true;
        target.Notoriety = Notoriety.Enemy;

        var result = service.Compute(source, target);

        Assert.That(result, Is.EqualTo(Notoriety.Invulnerable));
    }

    [Test]
    public void Compute_WhenTargetIsCriminal_ShouldReturnCriminal()
    {
        var service = new NotorietyService();
        var result = service.Compute(CreatePlayer(), new UOMobileEntity { Notoriety = Notoriety.Criminal });

        Assert.That(result, Is.EqualTo(Notoriety.Criminal));
    }

    [Test]
    public void Compute_WhenTargetIsMonsterWithEnemyTemplateNotoriety_ShouldReturnCanBeAttacked()
    {
        var service = new NotorietyService();
        var target = CreateMonster();
        target.Notoriety = Notoriety.Enemy;

        var result = service.Compute(CreatePlayer(), target);

        Assert.That(result, Is.EqualTo(Notoriety.CanBeAttacked));
    }

    [Test]
    public void Compute_WhenTargetIsPlayerEnemy_ShouldPreserveEnemy()
    {
        var service = new NotorietyService();
        var target = new UOMobileEntity
        {
            Notoriety = Notoriety.Enemy,
            BaseBody = 0x0190,
            IsPlayer = true
        };

        var result = service.Compute(CreatePlayer(), target);

        Assert.That(result, Is.EqualTo(Notoriety.Enemy));
    }

    [Test]
    public void Compute_WhenViewerHasRecentAggressorAgainstTarget_ShouldReturnCanBeAttacked()
    {
        var service = new NotorietyService();
        var viewer = CreatePlayer((Serial)0x0100u, Notoriety.Innocent);
        var target = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);

        viewer.RefreshAggressor(target.Id, viewer.Id, DateTime.UtcNow);

        var result = service.Compute(viewer, target);

        Assert.That(result, Is.EqualTo(Notoriety.CanBeAttacked));
    }

    [Test]
    public void Compute_WhenAggressionEntryIsExpired_ShouldReturnInnocent()
    {
        var service = new NotorietyService();
        var viewer = CreatePlayer((Serial)0x0100u, Notoriety.Innocent);
        var target = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);

        viewer.Aggressors.Add(new(target.Id, viewer.Id, DateTime.UtcNow.AddMinutes(-3), false, false));

        var result = service.Compute(viewer, target);

        Assert.That(result, Is.EqualTo(Notoriety.Innocent));
    }

    [Test]
    public void Compute_WhenViewerTargetsSelf_ShouldReturnInnocent()
    {
        var service = new NotorietyService();
        var viewer = CreatePlayer((Serial)0x0100u, Notoriety.Criminal);

        var result = service.Compute(viewer, viewer);

        Assert.That(result, Is.EqualTo(Notoriety.Innocent));
    }

    [Test]
    public void Compute_WhenTargetBelongsToEnemyFaction_ShouldReturnEnemy()
    {
        var service = new NotorietyService(CreateFactionTemplateService());
        var viewer = CreatePlayer((Serial)0x0100u, Notoriety.Innocent);
        var target = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);
        viewer.FactionId = "true_britannians";
        target.FactionId = "shadowlords";

        var result = service.Compute(viewer, target);

        Assert.That(result, Is.EqualTo(Notoriety.Enemy));
    }

    [Test]
    public void Compute_WhenTargetBelongsToSameFaction_ShouldReturnInnocent()
    {
        var service = new NotorietyService(CreateFactionTemplateService());
        var viewer = CreatePlayer((Serial)0x0100u, Notoriety.Innocent);
        var target = CreatePlayer((Serial)0x0101u, Notoriety.Innocent);
        viewer.FactionId = "true_britannians";
        target.FactionId = "true_britannians";

        var result = service.Compute(viewer, target);

        Assert.That(result, Is.EqualTo(Notoriety.Innocent));
    }

    private static UOMobileEntity CreatePlayer()
        => new()
        {
            IsPlayer = true,
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

    private static UOMobileEntity CreateMonster()
        => new()
        {
            IsPlayer = false,
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
