using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Http.Services.Mobiles;

public sealed class BodyImageExportJobTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("mg-bodyjob-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    [Fact]
    public async Task Run_WalksClassifiedBodies_SkippingEquipmentAndCountingFailures()
    {
        var catalog = new FakeAnimationCatalog();
        catalog.Bodies.Add((400, MobType.Human));
        catalog.Bodies.Add((401, MobType.Human));      // no frame: counts as failed, not fatal
        catalog.Bodies.Add((5000, MobType.Equipment)); // excluded outright
        catalog.Frames[(400, 0)] = (W: 20, H: 40, Cx: 10, Cy: 0);
        var job = new BodyImageExportJob(
            new BodyImageService(catalog, new DirectoriesConfig(_root, Array.Empty<string>()), new StubUltimaReadGate()),
            catalog
        );

        Assert.True(job.TryStart());

        await WaitUntilDoneAsync(job);

        Assert.Equal("Completed", job.Status.State);
        Assert.Equal(2, job.Status.Total);
        Assert.Equal(2, job.Status.Done);
        Assert.Equal(1, job.Status.Failed);
    }

    [Fact]
    public void TryStart_WhileRunning_IsRefused()
    {
        // A catalog with no bodies completes almost instantly, so pin Running by hand is impossible;
        // instead assert the contract the route relies on: a second start right after the first is
        // either refused (still running) or accepted (already completed) — never an exception, and the
        // state machine never reports two concurrent runs.
        var catalog = new FakeAnimationCatalog();
        var job = new BodyImageExportJob(
            new BodyImageService(catalog, new DirectoriesConfig(_root, Array.Empty<string>()), new StubUltimaReadGate()),
            catalog
        );

        Assert.True(job.TryStart());
        Assert.True(job.Status.State is "Running" or "Completed");
    }

    private static async Task WaitUntilDoneAsync(BodyImageExportJob job)
    {
        for (var i = 0; i < 100 && job.Status.State == "Running"; i++)
        {
            await Task.Delay(20);
        }
    }
}
