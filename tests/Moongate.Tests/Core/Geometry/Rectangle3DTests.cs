using System.Globalization;

using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Rectangle3DTests
{
    [Theory]
    [InlineData(10, 20, -5, true)]
    [InlineData(13, 24, 0, true)]
    [InlineData(14, 20, -5, false)]
    [InlineData(10, 25, -5, false)]
    [InlineData(10, 20, 1, false)]
    [InlineData(10, 20, -6, false)]
    public void Contains_UsesInclusiveStartAndExclusiveEnd(int x, int y, int z, bool expected)
    {
        var rectangle = new Rectangle3D(10, 20, -5, 4, 5, 6);
        var point = new Point3D(x, y, z);
        Assert.Equal(expected, rectangle.Contains(point));
        Assert.Equal(expected, rectangle.Contains((IPoint3D)point));
    }

    [Fact]
    public void Contains_TwoDimensionalPoint_IgnoresElevation()
    {
        var rectangle = new Rectangle3D(10, 20, -5, 4, 5, 6);
        Assert.True(rectangle.Contains(new Point2D(10, 20)));
        Assert.True(rectangle.Contains((IPoint2D)new Point3D(10, 20, 500)));
        Assert.False(rectangle.Contains((IPoint2D)new Point2D(14, 20)));
        Assert.False(default(Rectangle3D).Contains(Point3D.Zero));
    }

    [Theory]
    [InlineData("(10, 20, -5)+(4, 5, 6)")]
    [InlineData(" (+10, +20, -5) + (+4, +5, +6) ")]
    public void Parse_PositionAndSize_ProducesExpectedBounds(string text)
    {
        var expected = new Rectangle3D(new Point3D(10, 20, -5), new Point3D(14, 25, 1));
        Assert.Equal(expected, Rectangle3D.Parse(text));
        Assert.True(Rectangle3D.TryParse(text, null, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(expected, Rectangle3D.Parse(expected.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("(1,2,3)")]
    [InlineData("(1,2,3)+(4,5)")]
    [InlineData("(1,2,3)+(4,5,6)+(7,8,9)")]
    public void TryParse_InvalidBounds_ReturnsFalseAndDefault(string? text)
    {
        Assert.False(Rectangle3D.TryParse(text, null, out var result));
        Assert.Equal(default, result);
        Assert.Throws<FormatException>(() => Rectangle3D.Parse(text!));
    }

    [Fact]
    public void MakeHold_ExpandsAllDimensionsAndMaintainsValueEquality()
    {
        var rectangle = new Rectangle3D(10, 20, -5, 4, 5, 6);
        rectangle.MakeHold(new Rectangle3D(8, 22, -10, 10, 10, 20));
        var expected = new Rectangle3D(8, 20, -10, 10, 12, 20);
        Assert.Equal(expected, rectangle);
        Assert.True(rectangle == expected);
        Assert.False(rectangle != expected);
        Assert.True(rectangle.Equals((object)expected));
        Assert.Equal(expected.GetHashCode(), rectangle.GetHashCode());
        Assert.Equal(new Point3D(18, 32, 10), rectangle.End);
        Assert.Equal(20, rectangle.Depth);
    }

    [Fact]
    public void Formatting_UsesProviderAndRejectsSmallDestination()
    {
        var rectangle = new Rectangle3D(-1, 2, 3, 4, 5, 6);
        var provider = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        provider.NegativeSign = "minus";
        Assert.Equal("(minus1, 2, 3)+(4, 5, 6)", rectangle.ToString(null, provider));
        Span<char> destination = stackalloc char[32];
        Assert.True(rectangle.TryFormat(destination, out var written, default, provider));
        Assert.Equal("(minus1, 2, 3)+(4, 5, 6)", destination[..written].ToString());
        Assert.False(rectangle.TryFormat(destination[..2], out written, default, provider));
        Assert.Equal(0, written);
    }
}
