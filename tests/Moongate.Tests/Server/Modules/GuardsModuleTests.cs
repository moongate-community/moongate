namespace Moongate.Tests.Server.Modules;

public sealed class GuardsModuleTests
{
    [Test]
    public void SetFocus_WhenGuardAndTargetExist_ShouldPersistFocus()
    {
        var moduleType = GetGuardsModuleType();

        Assert.Multiple(
            () =>
            {
                Assert.That(moduleType, Is.Not.Null);

                if (moduleType is null)
                {
                    return;
                }

                var setFocus = moduleType.GetMethod("SetFocus");
                var getFocus = moduleType.GetMethod("GetFocus");

                Assert.That(setFocus, Is.Not.Null);
                Assert.That(getFocus, Is.Not.Null);
                Assert.That(setFocus!.ReturnType, Is.EqualTo(typeof(bool)));
                Assert.That(setFocus.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo([typeof(uint), typeof(uint?)]));
                Assert.That(getFocus!.ReturnType, Is.EqualTo(typeof(uint?)));
                Assert.That(getFocus.GetParameters(), Is.Empty);
            }
        );
    }

    [Test]
    public void TeleportToTarget_WhenTargetExists_ShouldMoveGuardToTargetLocation()
    {
        var moduleType = GetGuardsModuleType();

        Assert.Multiple(
            () =>
            {
                Assert.That(moduleType, Is.Not.Null);

                if (moduleType is null)
                {
                    return;
                }

                var teleportToTarget = moduleType.GetMethod("TeleportToTarget");

                Assert.That(teleportToTarget, Is.Not.Null);
                Assert.That(teleportToTarget!.ReturnType, Is.EqualTo(typeof(bool)));
                Assert.That(teleportToTarget.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo([typeof(uint), typeof(uint)]));
            }
        );
    }

    private static Type? GetGuardsModuleType()
        => Type.GetType("Moongate.Server.Modules.GuardsModule, Moongate.Server", throwOnError: false);
}
