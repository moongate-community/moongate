using Moongate.Tests.Support;
using Moongate.Ultima;

using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class FilesTests
{
    [Fact]
    public void SetDirectory_MixedCaseFileNames_AreResolvedOnCaseSensitiveFilesystem()
    {
        string dir = UltimaFixtures.CreateClientDirectory(
            ("map0LegacyMUL.uop", [1]),
            ("artLegacyMUL.uop", [1]),
            ("tiledata.mul", [1]));

        try
        {
            Files.SetDirectory(dir);

            Assert.Equal(Path.Combine(dir, "map0LegacyMUL.uop"), Files.GetFilePath("map0legacymul.uop"));
            Assert.Equal(Path.Combine(dir, "artLegacyMUL.uop"), Files.GetFilePath("artlegacymul.uop"));
            Assert.Equal(Path.Combine(dir, "tiledata.mul"), Files.GetFilePath("tiledata.mul"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetFilePath_MissingFile_ReturnsNull()
    {
        string dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", [1]));

        try
        {
            Files.SetDirectory(dir);

            Assert.Null(Files.GetFilePath("hues.mul"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetDirectory_NonExistingDirectory_LeavesAllPathsEmpty()
    {
        Files.SetDirectory("/nonexistent/uo-client-dir");

        Assert.Null(Files.GetFilePath("tiledata.mul"));
    }
}
