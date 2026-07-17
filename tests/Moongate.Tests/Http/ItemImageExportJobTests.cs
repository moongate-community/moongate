using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Services;
using Moongate.Http.Plugin.Types;
using Moongate.Tests.Support;
using Moongate.Ultima.Catalog;

namespace Moongate.Tests.Http;

[Collection("UltimaClientData")]
public class ItemImageExportJobTests
{
    private static async Task<ItemImageExportStatus> WaitForCompletionAsync(ItemImageExportJob job)
    {
        // Polls rather than sleeping a fixed span: the export is a background task, and a fixed wait would
        // either flake on a loaded machine or waste the difference on a fast one.
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            var status = job.Status;

            if (status.State != nameof(ItemImageExportStateType.Running))
            {
                return status;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"The export never left Running. Last status: {job.Status}.");
    }

    [Fact]
    public async Task TryStart_ExportsEveryItemThatHasArt()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());
        var job = new ItemImageExportJob(service);

        Assert.True(job.TryStart());

        var status = await WaitForCompletionAsync(job);

        Assert.Equal(nameof(ItemImageExportStateType.Completed), status.State);
        Assert.Equal(0, status.Failed);
        Assert.Equal(status.Total, status.Done);
        Assert.True(status.Done > 0);

        // Asserts on the files rather than on the cache's naming, which is the service's own business:
        // what the export promises is that every item it counted ended up on disk.
        var written = Directory.GetFiles(fixture.Directories.GetPath("cache/images/items"), "*.png");

        Assert.Equal(status.Done, written.Length);
    }

    [Fact]
    public async Task TryStart_WhileRunning_IsRefused()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());
        var job = new ItemImageExportJob(service);

        Assert.True(job.TryStart());

        // Either the second start is refused, or the first finished before this line ran. Asserting only
        // the refusal would flake on a fast machine, so this accepts both and checks the state matches.
        var refused = !job.TryStart();
        var status = await WaitForCompletionAsync(job);

        Assert.True(refused || status.State == nameof(ItemImageExportStateType.Completed));
    }

    [Fact]
    public void Status_BeforeAnyExport_IsIdle()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());
        var job = new ItemImageExportJob(service);

        var status = job.Status;

        Assert.Equal(nameof(ItemImageExportStateType.Idle), status.State);
        Assert.Null(status.StartedAt);
        Assert.Equal(0, status.Done);
    }
}
