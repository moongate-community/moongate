using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Rectangle3DTests
{
    [Fact]
    public void Constructor_NormalizesSwappedCorners()
    {
        var r = new Rectangle3D(new(5, 6, 10), new(1, 2, -10));

        Assert.Equal(new(1, 2, -10), r.Start);
        Assert.Equal(new(5, 6, 10), r.End);
        Assert.Equal(4, r.Width);
        Assert.Equal(4, r.Height);
        Assert.Equal(20, r.Depth);
    }

    [Theory, InlineData(1, 2, -10, true), InlineData(4, 5, 9, true), InlineData(5, 6, 10, false),
     InlineData(1, 2, 10, false), InlineData(0, 2, 0, false)]

    // start corner: inclusive
    // last cell inside
    // end corner: exclusive
    public void Contains_UsesInclusiveStartExclusiveEnd(int x, int y, int z, bool expected)
    {
        var r = new Rectangle3D(new(1, 2, -10), new(5, 6, 10));

        Assert.Equal(expected, r.Contains(new(x, y, z)));
    }

    [Fact]
    public void Equality_SameBounds_AreEqual()
    {
        var a = new Rectangle3D(new(0, 0, 0), new(1, 1, 1));
        var b = new Rectangle3D(new(0, 0, 0), new(1, 1, 1));

        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void GetHashCode_EqualBounds_AreEqual()
    {
        var a = new Rectangle3D(new(0, 0, 0), new(2, 2, 2));
        var b = new Rectangle3D(new(0, 0, 0), new(2, 2, 2));

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ShowsStartAndSize()
    {
        var r = new Rectangle3D(new(1, 2, -10), new(5, 6, 10));

        Assert.Equal("(1, 2, -10)+(4, 4, 20)", r.ToString());
    }
}
