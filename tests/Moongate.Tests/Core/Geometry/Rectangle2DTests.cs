using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Rectangle2DTests
{
    [Fact]
    public void Constructor_FromCorners_NormalizesSwappedCorners()
    {
        var r = new Rectangle2D(new(15, 24), new(10, 20));

        Assert.Equal(new(10, 20), r.Start);
        Assert.Equal(new(15, 24), r.End);
    }

    [Fact]
    public void Constructor_FromSize_ExposesBounds()
    {
        var r = new Rectangle2D(10, 20, 5, 4);

        Assert.Equal(new(10, 20), r.Start);
        Assert.Equal(new(15, 24), r.End);
        Assert.Equal(5, r.Width);
        Assert.Equal(4, r.Height);
    }

    [Theory, InlineData(10, 20, true), InlineData(14, 23, true), InlineData(15, 24, false), InlineData(9, 20, false),
     InlineData(12, 24, false)]

    // top-left corner: inclusive
    // last cell inside
    // bottom-right corner: exclusive
    public void Contains_Point2D_UsesInclusiveStartExclusiveEnd(int x, int y, bool expected)
    {
        var r = new Rectangle2D(10, 20, 5, 4);

        Assert.Equal(expected, r.Contains(new Point2D(x, y)));
    }

    [Fact]
    public void Contains_Point3D_IgnoresZ()
    {
        var r = new Rectangle2D(0, 0, 2, 2);

        Assert.True(r.Contains(new(1, 1, 120)));
        Assert.False(r.Contains(new(2, 1, 0)));
    }

    [Fact]
    public void Equality_SameBounds_AreEqual()
    {
        Assert.True(new Rectangle2D(1, 2, 3, 4) == new Rectangle2D(1, 2, 3, 4));
        Assert.True(new Rectangle2D(1, 2, 3, 4) != new Rectangle2D(1, 2, 3, 5));
    }

    [Fact]
    public void GetHashCode_EqualBounds_AreEqual()
        => Assert.Equal(new Rectangle2D(1, 2, 3, 4).GetHashCode(), new Rectangle2D(1, 2, 3, 4).GetHashCode());

    [Fact]
    public void ToString_ShowsOriginAndSize()
        => Assert.Equal("(1, 2)+(3, 4)", new Rectangle2D(1, 2, 3, 4).ToString());
}
