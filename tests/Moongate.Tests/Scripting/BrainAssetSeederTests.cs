using Moongate.Scripting.AI;

namespace Moongate.Tests.Scripting;

public class BrainAssetSeederTests
{
    [Fact]
    public void SeedMissing_EmptyDirectory_SeedsExactlyBuiltInBrainsAndCleansTemporaryFiles()
    {
        var root = CreateRoot();
        var brainsDirectory = Path.Combine(root, "scripts", "brains");

        try
        {
            BrainAssetSeeder.SeedMissing(brainsDirectory);

            var files = Directory
                        .GetFiles(brainsDirectory)
                        .Select(Path.GetFileName)
                        .Order(StringComparer.Ordinal)
                        .ToArray();

            Assert.Equal(["guard.lua", "orion.lua", "vega.lua"], files);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SeedMissing_ExistingEditedBrain_PreservesBytesAndSeedsOtherBuiltIns()
    {
        var root = CreateRoot();
        var brainsDirectory = Path.Combine(root, "scripts", "brains");
        Directory.CreateDirectory(brainsDirectory);
        var guardPath = Path.Combine(brainsDirectory, "guard.lua");
        var editedBytes = new byte[] { 0, 1, 2, 3, 255 };
        File.WriteAllBytes(guardPath, editedBytes);

        try
        {
            BrainAssetSeeder.SeedMissing(brainsDirectory);

            Assert.Equal(editedBytes, File.ReadAllBytes(guardPath));
            Assert.True(File.Exists(Path.Combine(brainsDirectory, "orion.lua")));
            Assert.True(File.Exists(Path.Combine(brainsDirectory, "vega.lua")));
            Assert.DoesNotContain(
                Directory.GetFiles(brainsDirectory),
                path => Path.GetFileName(path).Contains(".tmp", StringComparison.Ordinal)
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-brain-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        return root;
    }
}
