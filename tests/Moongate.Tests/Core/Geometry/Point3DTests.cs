using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Point3DTests
{
    [Fact]
    public void Constructor_And_Deconstruct_RoundTrip()
    {
        var p = new Point3D(1496, 1628, 10);
        var (x, y, z) = p;

        Assert.Equal((1496, 1628, 10), (x, y, z));
    }

    [Fact]
    public void Constructor_FromPoint2DAndZ_SetsAllCoordinates()
    {
        var p = new Point3D(new Point2D(5, 6), -3);

        Assert.Equal(new Point3D(5, 6, -3), p);
    }

    [Fact]
    public void Equality_ConsidersZ()
    {
        Assert.True(new Point3D(1, 2, 3) == new Point3D(1, 2, 3));
        Assert.True(new Point3D(1, 2, 3) != new Point3D(1, 2, 4));
    }

    [Fact]
    public void CompareTo_OrdersByXThenYThenZ()
    {
        Assert.True(new Point3D(1, 0, 0).CompareTo(new Point3D(2, 0, 0)) < 0);
        Assert.True(new Point3D(1, 1, 0).CompareTo(new Point3D(1, 0, 9)) > 0);
        Assert.True(new Point3D(1, 1, 1).CompareTo(new Point3D(1, 1, 2)) < 0);
    }

    [Fact]
    public void DistanceTo_IgnoresZ()
    {
        var ground = new Point3D(10, 10, 0);
        var roof = new Point3D(13, 11, 40);

        Assert.Equal(3, ground.DistanceTo(roof));
    }

    [Fact]
    public void ToPoint2D_And_ImplicitConversion_DropZ()
    {
        var p3 = new Point3D(7, 8, 9);
        Point2D viaImplicit = p3;

        Assert.Equal(new Point2D(7, 8), p3.ToPoint2D());
        Assert.Equal(new Point2D(7, 8), viaImplicit);
    }

    [Fact]
    public void ToString_And_Parse_RoundTrip()
    {
        var p = new Point3D(-1, 2, -3);

        Assert.Equal("(-1, 2, -3)", p.ToString());
        Assert.Equal(p, Point3D.Parse(p.ToString(), null));
    }

    [Theory]
    [InlineData("(1, 2, 3)", 1, 2, 3)]
    [InlineData(" ( 1 ,2, 3 ) ", 1, 2, 3)]
    public void TryParse_ValidInput_ReturnsPoint(string input, int x, int y, int z)
    {
        Assert.True(Point3D.TryParse(input, null, out var p));
        Assert.Equal(new Point3D(x, y, z), p);
    }

    [Theory]
    [InlineData("(1, 2)")]
    [InlineData("(1, 2, 3, 4)")]
    [InlineData("1, 2, 3")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        Assert.False(Point3D.TryParse(input, null, out _));
    }

    [Fact]
    public void GetHashCode_EqualValues_AreEqual()
    {
        Assert.Equal(new Point3D(1, 2, 3).GetHashCode(), new Point3D(1, 2, 3).GetHashCode());
    }

    [Fact]
    public void InRange_UsesChebyshevDistance_IgnoringZ()
    {
        var origin = new Point3D(100, 100, 0);

        Assert.True(origin.InRange(new Point3D(103, 98, 50), 3));
        Assert.False(origin.InRange(new Point3D(104, 98, 0), 3));
    }
}
