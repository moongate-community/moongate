using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;
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

    private static UOMobileEntity CreatePlayer()
        => new()
        {
            IsPlayer = true,
            Notoriety = Notoriety.Innocent,
            BaseBody = 0x0190
        };

    private static UOMobileEntity CreateMonster()
        => new()
        {
            IsPlayer = false,
            BaseBody = 0x0003
        };
}
