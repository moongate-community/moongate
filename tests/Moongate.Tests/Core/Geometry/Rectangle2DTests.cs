using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Rectangle2DTests
{
    [Fact]
    public void Constructor_FromSize_ExposesBounds()
    {
        var r = new Rectangle2D(10, 20, 5, 4);

        Assert.Equal(new Point2D(10, 20), r.Start);
        Assert.Equal(new Point2D(15, 24), r.End);
        Assert.Equal(5, r.Width);
        Assert.Equal(4, r.Height);
    }

    [Fact]
    public void Constructor_FromCorners_NormalizesSwappedCorners()
    {
        var r = new Rectangle2D(new Point2D(15, 24), new Point2D(10, 20));

        Assert.Equal(new Point2D(10, 20), r.Start);
        Assert.Equal(new Point2D(15, 24), r.End);
    }

    [Theory]
    [InlineData(10, 20, true)]   // top-left corner: inclusive
    [InlineData(14, 23, true)]   // last cell inside
    [InlineData(15, 24, false)]  // bottom-right corner: exclusive
    [InlineData(9, 20, false)]
    [InlineData(12, 24, false)]
    public void Contains_Point2D_UsesInclusiveStartExclusiveEnd(int x, int y, bool expected)
    {
        var r = new Rectangle2D(10, 20, 5, 4);

        Assert.Equal(expected, r.Contains(new Point2D(x, y)));
    }

    [Fact]
    public void Contains_Point3D_IgnoresZ()
    {
        var r = new Rectangle2D(0, 0, 2, 2);

        Assert.True(r.Contains(new Point3D(1, 1, 120)));
        Assert.False(r.Contains(new Point3D(2, 1, 0)));
    }

    [Fact]
    public void Equality_SameBounds_AreEqual()
    {
        Assert.True(new Rectangle2D(1, 2, 3, 4) == new Rectangle2D(1, 2, 3, 4));
        Assert.True(new Rectangle2D(1, 2, 3, 4) != new Rectangle2D(1, 2, 3, 5));
    }

    [Fact]
    public void ToString_ShowsOriginAndSize()
    {
        Assert.Equal("(1, 2)+(3, 4)", new Rectangle2D(1, 2, 3, 4).ToString());
    }
}
