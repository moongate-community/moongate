using Moongate.Server.Services.Localization;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Server.Localization;

/// <summary>
/// The cliloc table is what lets server-side surfaces show a name for an item the shard deliberately
/// does not name. Files is a process-wide static, hence the serialized collection.
/// </summary>
[Collection("UltimaClientData")]
public class ClilocServiceTests
{
    // A shard whose client directory has no cliloc for the configured language still boots; every
    // lookup simply answers null and callers fall back.
    [Fact]
    public async Task Start_WithNoClilocFile_DoesNotThrowAndAnswersNull()
    {
        var dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", UltimaFixtures.BuildTileData()));

        try
        {
            Files.SetDirectory(dir);

            var service = new ClilocService(new() { Language = "enu" });

            await service.StartAsync();

            Assert.Null(service.Text(1023821));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Text_AfterStart_ReturnsTheEntry()
        => await WithClientFiles(
               async service =>
               {
                   await service.StartAsync();

                   Assert.Equal("gold coin", service.Text(1023821));
                   Assert.Equal("platemail legs", service.Text(1025137));
               }
           );

    [Fact]
    public void Text_BeforeStart_IsNull()
    {
        var service = new ClilocService(new() { Language = "enu" });

        Assert.Null(service.Text(1023821));
    }

    [Fact]
    public async Task Text_ClilocTheTableDoesNotHold_IsNull()
        => await WithClientFiles(
               async service =>
               {
                   await service.StartAsync();

                   Assert.Null(service.Text(1099999));
               }
           );

    private static async Task WithClientFiles(Func<ClilocService, Task> assert)
    {
        var cliloc = UltimaFixtures.BuildCliloc((1023821, "gold coin"), (1025137, "platemail legs"));
        var dir = UltimaFixtures.CreateClientDirectory(("cliloc.enu", cliloc));

        try
        {
            Files.SetDirectory(dir);

            await assert(new(new() { Language = "enu" }));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
