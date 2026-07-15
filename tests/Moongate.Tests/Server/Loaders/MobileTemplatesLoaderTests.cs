using Moongate.Server.Loaders;
using Moongate.Server.Services.Mobiles;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server.Loaders;

public class MobileTemplatesLoaderTests
{
    [Fact]
    public async Task LoadAsync_LoadsYamlRecursivelyAndResolvesBase()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, []);
        var mobilesDirectory = Path.Combine(directories.RegisterDirectory("templates"), "mobiles");
        Directory.CreateDirectory(Path.Combine(mobilesDirectory, "base"));

        File.WriteAllText(
            Path.Combine(mobilesDirectory, "base", "human.yaml"),
            """
            -   Id: base_human
                Name: Human
                Strength: 80
                Appearance:
                    Body: 400
            """
        );
        File.WriteAllText(
            Path.Combine(mobilesDirectory, "guards.yaml"),
            """
            -   Id: town_guard
                BaseMobile: base_human
                Name: Town Guard
                Strength: 100
                Skills:
                    Swordsmanship: 900
            """
        );

        var service = new MobileTemplateService();

        try
        {
            await new MobileTemplatesLoader(service, directories).LoadAsync();

            Assert.Equal(2, service.Count);
            var guard = service.GetById("town_guard")!;
            Assert.Equal("Town Guard", guard.Name);
            Assert.Equal(100, guard.Strength);
            Assert.Equal(400, guard.Appearance.Body); // inherited from base
            Assert.Equal(900, guard.Skills["Swordsmanship"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_EmptyExistingDirectory_RegistersNothing()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, []);
        Directory.CreateDirectory(Path.Combine(directories.RegisterDirectory("templates"), "mobiles"));
        var service = new MobileTemplateService();

        try
        {
            await new MobileTemplatesLoader(service, directories).LoadAsync();
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "moongate-mobile-templates-" + Guid.NewGuid().ToString("N"));
}
