using Moongate.Server.Services.Scripting;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class NpcAiRuntimeStateServiceTests
{
    [Test]
    public void BindPromptFile_ShouldStorePromptPerNpc()
    {
        var service = new NpcAiRuntimeStateService();
        var serial = (Serial)0x100u;

        service.BindPromptFile(serial, "lilly.txt");
        var found = service.TryGetPromptFile(serial, out var promptFile);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(promptFile, Is.EqualTo("lilly.txt"));
            }
        );
    }

    [Test]
    public void TryAcquireIdle_ShouldRespectCooldown()
    {
        var service = new NpcAiRuntimeStateService();
        var serial = (Serial)0x101u;

        var first = service.TryAcquireIdle(serial, 10_000, 60_000);
        var second = service.TryAcquireIdle(serial, 20_000, 60_000);
        var third = service.TryAcquireIdle(serial, 70_000, 60_000);

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
                Assert.That(third, Is.True);
            }
        );
    }

    [Test]
    public void TryAcquireListener_ShouldRespectCooldown()
    {
        var service = new NpcAiRuntimeStateService();
        var serial = (Serial)0x100u;

        var first = service.TryAcquireListener(serial, 1_000, 5_000);
        var second = service.TryAcquireListener(serial, 2_000, 5_000);
        var third = service.TryAcquireListener(serial, 6_000, 5_000);

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
                Assert.That(third, Is.True);
            }
        );
    }
}
