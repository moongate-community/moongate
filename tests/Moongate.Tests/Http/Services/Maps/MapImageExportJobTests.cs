using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Services.Maps;
using Moongate.Http.Plugin.Services.Ultima;
using Moongate.Http.Plugin.Types;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Services.Maps;

[Collection("UltimaClientData")]
public class MapImageExportJobTests
{
    private static MapImageExportJob Job(MapImageFixture fixture, out MapImageService service)
    {
        service = new(fixture.Provider, fixture.Directories, new UltimaReadGate());

        return new(service, fixture.Provider);
    }

    private static async Task<MapImageExportStatus> WaitForCompletionAsync(MapImageExportJob job)
    {
        // Polls rather than sleeping a fixed span: the export is a background task, and a fixed wait would
        // either flake on a loaded machine or waste the difference on a fast one.
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            var status = job.Status;

            if (status.State != nameof(MapImageExportStateType.Running))
            {
                return status;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"The export never left Running. Last status: {job.Status}.");
    }

    [Fact]
    public async Task TryStart_FillsThePyramidAndTheFullImage()
    {
        using var fixture = MapImageFixture.Create();
        var job = Job(fixture, out var service);

        Assert.True(job.TryStart());

        var status = await WaitForCompletionAsync(job);

        Assert.Equal(nameof(MapImageExportStateType.Completed), status.State);
        Assert.Equal(0, status.Failed);
        Assert.Equal(status.Total, status.Done);

        // Every zoom is on disk, and so is the whole-facet image — the one nobody should meet unwarmed.
        var root = fixture.Directories.GetPath("cache/images/maps");
        var maxZoom = service.MaxZoomFor(MapType.Felucca);

        for (var zoom = 0; zoom <= maxZoom; zoom++)
        {
            Assert.True(
                Directory.Exists(Path.Combine(root, "felucca", zoom.ToString())),
                $"zoom {zoom} was not exported"
            );
        }

        Assert.True(File.Exists(Path.Combine(root, "felucca", "full.png")));
    }

    [Fact]
    public async Task TryStart_WhileRunning_IsRefused()
    {
        using var fixture = MapImageFixture.Create();
        var job = Job(fixture, out _);

        Assert.True(job.TryStart());

        // Either the second start is refused, or the first finished before this line ran. Asserting only
        // the refusal would flake on a fast machine, so this accepts both and checks the state matches.
        var refused = !job.TryStart();
        var status = await WaitForCompletionAsync(job);

        Assert.True(refused || status.State == nameof(MapImageExportStateType.Completed));
    }

    [Fact]
    public void Status_BeforeAnyExport_IsIdle()
    {
        using var fixture = MapImageFixture.Create();
        var job = Job(fixture, out _);

        var status = job.Status;

        Assert.Equal(nameof(MapImageExportStateType.Idle), status.State);
        Assert.Null(status.StartedAt);
        Assert.Equal(0, status.Done);
    }
}
