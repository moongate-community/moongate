using Moongate.Http.Plugin.Services.Mobiles;

namespace Moongate.Tests.Http.Services.Mobiles;

public class EquipConvTableTests
{
    [Fact]
    public void MissingFile_YieldsAnEmptyTable()
    {
        var table = new EquipConvTable(Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.def"));

        Assert.Equal(0, table.Count);
        Assert.False(table.TryConvert(400, 0x1DB, out _));
    }

    [Fact]
    public void ParsesEntries_AndIgnoresCommentsAndMalformedLines()
    {
        var path = Path.Combine(Path.GetTempPath(), $"equipconv-{Guid.NewGuid():N}.def");
        File.WriteAllLines(
            path,
            [
                "# comment",
                "\"header line\"",
                "400 475 267 10265 0 # sword fits body 400",
                "401 475 267 10265 1153",
                "garbage without numbers"
            ]
        );

        try
        {
            var table = new EquipConvTable(path);

            Assert.Equal(2, table.Count);
            Assert.True(table.TryConvert(400, 475, out var plain));
            Assert.Equal((267, 0), plain);
            Assert.True(table.TryConvert(401, 475, out var hued));
            Assert.Equal((267, 1153), hued);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
