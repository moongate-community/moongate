using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Point2DTests
{
    [Fact]
    public void CompareTo_OrdersByXThenY()
    {
        Assert.True(new Point2D(1, 9).CompareTo(new(2, 0)) < 0);
        Assert.True(new Point2D(2, 1).CompareTo(new(2, 0)) > 0);
        Assert.Equal(0, new Point2D(2, 2).CompareTo(new(2, 2)));
    }

    [Fact]
    public void Constructor_And_Deconstruct_RoundTrip()
    {
        var p = new Point2D(1496, 1628);
        var (x, y) = p;

        Assert.Equal(1496, p.X);
        Assert.Equal(1628, p.Y);
        Assert.Equal((1496, 1628), (x, y));
    }

    [Theory, InlineData(0, 0, 0, 0, 0), InlineData(0, 0, 3, 1, 3), InlineData(5, 5, 2, 9, 4), InlineData(-1, -1, 1, 1, 2)]

    // Chebyshev: max(|dx|, |dy|)
    public void DistanceTo_IsChebyshev(int x1, int y1, int x2, int y2, int expected)
        => Assert.Equal(expected, new Point2D(x1, y1).DistanceTo(new(x2, y2)));

    [Fact]
    public void Equality_SameCoordinates_AreEqual()
    {
        var a = new Point2D(3, 7);
        var b = new Point2D(3, 7);

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a != b);
        Assert.True(a != new Point2D(3, 8));
    }

    [Fact]
    public void InRange_UsesChebyshevDistance()
    {
        var origin = new Point2D(100, 100);

        Assert.True(origin.InRange(new(103, 98), 3));
        Assert.False(origin.InRange(new(104, 98), 3));
    }

    [Fact]
    public void Parse_InvalidInput_Throws()
        => Assert.Throws<FormatException>(() => Point2D.Parse("nope", null));

    [Fact]
    public void ToString_And_Parse_RoundTrip()
    {
        var p = new Point2D(-12, 34);

        Assert.Equal("(-12, 34)", p.ToString());
        Assert.Equal(p, Point2D.Parse(p.ToString(), null));
    }

    [Theory, InlineData(""), InlineData("1, 2"), InlineData("(1; 2)"), InlineData("(1)"), InlineData("(a, 2)")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
        => Assert.False(Point2D.TryParse(input, null, out _));

    [Theory, InlineData("(1, 2)", 1, 2), InlineData(" ( 1 , 2 ) ", 1, 2), InlineData("(-5,7)", -5, 7)]
    public void TryParse_ValidInput_ReturnsPoint(string input, int x, int y)
    {
        Assert.True(Point2D.TryParse(input, null, out var p));
        Assert.Equal(new(x, y), p);
    }

    [Fact]
    public void Zero_IsOrigin()
        => Assert.Equal(new(0, 0), Point2D.Zero);
}
