using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Files;
using Moongate.UO.Data.Services.Art;

namespace Moongate.Tests.UO.Data.Art;

[NonParallelizable]
public class ArtServiceTests
{
    private sealed class UoFilesScope : IDisposable
    {
        private readonly Dictionary<string, string> _snapshot;
        private readonly string _rootSnapshot;

        public UoFilesScope(string idxPath, string mulPath)
        {
            _snapshot = UoFiles.MulPath.ToDictionary(pair => pair.Key, pair => pair.Value);
            _rootSnapshot = UoFiles.RootDir;

            UoFiles.MulPath.Clear();
            UoFiles.SetMulPath(idxPath, "artidx.mul");
            UoFiles.SetMulPath(mulPath, "art.mul");
        }

        public void Dispose()
        {
            UoFiles.MulPath.Clear();

            foreach (var pair in _snapshot)
            {
                UoFiles.MulPath[pair.Key] = pair.Value;
            }

            UoFiles.RootDir = _rootSnapshot;
        }
    }

    [Test]
    public void GetArt_WhenCloneIsFalse_ShouldReturnCachedInstance()
    {
        using var temp = new TempDirectory();
        var files = CreateTestArtFiles(temp.Path);
        using var scope = new UoFilesScope(files.IdxPath, files.MulPath);
        var fileIndex = new FileIndex("artidx.mul", "art.mul", 0x5000, -1);
        var service = new ArtService(fileIndex);

        var first = service.GetArt(0, false);
        var second = service.GetArt(0, false);

        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void GetArt_WhenCloneIsTrue_ShouldReturnDistinctBitmaps()
    {
        using var temp = new TempDirectory();
        var files = CreateTestArtFiles(temp.Path);
        using var scope = new UoFilesScope(files.IdxPath, files.MulPath);
        var fileIndex = new FileIndex("artidx.mul", "art.mul", 0x5000, -1);
        var service = new ArtService(fileIndex);

        using var first = service.GetArt(0);
        using var second = service.GetArt(0);

        Assert.That(ReferenceEquals(first, second), Is.False);
    }

    [Test]
    public void GetArt_WhenEntryExists_ShouldDecodeBitmap()
    {
        using var temp = new TempDirectory();
        var files = CreateTestArtFiles(temp.Path);
        using var scope = new UoFilesScope(files.IdxPath, files.MulPath);
        var fileIndex = new FileIndex("artidx.mul", "art.mul", 0x5000, -1);
        var service = new ArtService(fileIndex);

        using var image = service.GetArt(0);

        Assert.Multiple(
            () =>
            {
                Assert.That(image, Is.Not.Null);
                Assert.That(image!.Width, Is.EqualTo(1));
                Assert.That(image.Height, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void IsValidArt_WhenEntryMissing_ShouldReturnFalse()
    {
        using var temp = new TempDirectory();
        var files = CreateTestArtFiles(temp.Path);
        using var scope = new UoFilesScope(files.IdxPath, files.MulPath);
        var fileIndex = new FileIndex("artidx.mul", "art.mul", 0x5000, -1);
        var service = new ArtService(fileIndex);

        var isValid = service.IsValidArt(1234);

        Assert.That(isValid, Is.False);
    }

    private static (string IdxPath, string MulPath) CreateTestArtFiles(string rootPath)
    {
        var idxPath = Path.Combine(rootPath, "artidx.mul");
        var mulPath = Path.Combine(rootPath, "art.mul");
        var entries = 0x5000;
        var staticIndex = 0x4000;

        using (var idxWriter = new BinaryWriter(File.Open(idxPath, FileMode.Create, FileAccess.Write, FileShare.None)))
        {
            for (var i = 0; i < entries; i++)
            {
                if (i == staticIndex)
                {
                    idxWriter.Write(0);  // lookup
                    idxWriter.Write(20); // length
                    idxWriter.Write(0);  // extra
                }
                else
                {
                    idxWriter.Write(-1);
                    idxWriter.Write(-1);
                    idxWriter.Write(-1);
                }
            }
        }

        using (var mulWriter = new BinaryWriter(File.Open(mulPath, FileMode.Create, FileAccess.Write, FileShare.None)))
        {
            // static art format:
            // [ushort unk][ushort unk][ushort width][ushort height][ushort lookup...][run data...]
            // 1x1 pixel with one run in first row.
            var words = new ushort[]
            {
                0,
                0,
                1,
                1,
                0,      // lookup for row 0 -> start + 0
                0,      // xOffset
                1,      // xRun
                0x001F, // pixel (blue-ish in 5:5:5)
                0,      // terminator xOffset
                0       // terminator xRun
            };

            foreach (var word in words)
            {
                mulWriter.Write(word);
            }
        }

        return (idxPath, mulPath);
    }
}
