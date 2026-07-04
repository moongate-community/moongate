using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Rectangle3DTests
{
    [Fact]
    public void Constructor_NormalizesSwappedCorners()
    {
        var r = new Rectangle3D(new Point3D(5, 6, 10), new Point3D(1, 2, -10));

        Assert.Equal(new Point3D(1, 2, -10), r.Start);
        Assert.Equal(new Point3D(5, 6, 10), r.End);
        Assert.Equal(4, r.Width);
        Assert.Equal(4, r.Height);
        Assert.Equal(20, r.Depth);
    }

    [Theory]
    [InlineData(1, 2, -10, true)]   // start corner: inclusive
    [InlineData(4, 5, 9, true)]     // last cell inside
    [InlineData(5, 6, 10, false)]   // end corner: exclusive
    [InlineData(1, 2, 10, false)]
    [InlineData(0, 2, 0, false)]
    public void Contains_UsesInclusiveStartExclusiveEnd(int x, int y, int z, bool expected)
    {
        var r = new Rectangle3D(new Point3D(1, 2, -10), new Point3D(5, 6, 10));

        Assert.Equal(expected, r.Contains(new Point3D(x, y, z)));
    }

    [Fact]
    public void Equality_SameBounds_AreEqual()
    {
        var a = new Rectangle3D(new Point3D(0, 0, 0), new Point3D(1, 1, 1));
        var b = new Rectangle3D(new Point3D(0, 0, 0), new Point3D(1, 1, 1));

        Assert.True(a == b);
        Assert.False(a != b);
    }
}
