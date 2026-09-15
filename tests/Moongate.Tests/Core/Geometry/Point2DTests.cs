using System.Globalization;

using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Point2DTests
{
    [Theory]
    [InlineData("(12, -34)", 12, -34)]
    [InlineData("  ( +12 , -34 )  ", 12, -34)]
    [InlineData("(-2147483648, 2147483647)", int.MinValue, int.MaxValue)]
    public void Parse_ValidCoordinates_ReadsStringAndSpan(string text, int x, int y)
    {
        var expected = new Point2D(x, y);
        Assert.Equal(expected, Point2D.Parse(text));
        Assert.Equal(expected, Point2D.Parse(text.AsSpan(), CultureInfo.InvariantCulture));
        Assert.True(Point2D.TryParse(text, null, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("()")]
    [InlineData("1,2")]
    [InlineData("(1)")]
    [InlineData("(1,2,3)")]
    [InlineData("(1,)")]
    [InlineData("(2147483648,0)")]
    [InlineData("(x,2)")]
    public void TryParse_InvalidCoordinates_ReturnsFalseAndDefault(string? text)
    {
        Assert.False(Point2D.TryParse(text, null, out var point));
        Assert.Equal(Point2D.Zero, point);
        Assert.Throws<FormatException>(() => Point2D.Parse(text!));
    }

    [Fact]
    public void Formatting_UsesCoordinatesAndRejectsSmallDestination()
    {
        var point = new Point2D(-12, 34);
        Assert.Equal("(-12, 34)", point.ToString());
        Span<char> destination = stackalloc char[9];
        Assert.True(point.TryFormat(destination, out var written, default, null));
        Assert.Equal("(-12, 34)", destination[..written].ToString());
        Assert.False(point.TryFormat(destination[..3], out written, default, null));
        Assert.Equal(0, written);
    }

    [Fact]
    public void Formatting_ExplicitProvider_RoundTripsCustomNegativeSign()
    {
        var provider = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        provider.NegativeSign = "minus";
        var point = new Point2D(-12, 34);
        var text = point.ToString(null, provider);
        Assert.Equal("(minus12, 34)", text);
        Assert.Equal(point, Point2D.Parse(text, provider));
    }

    [Fact]
    public void Equality_InterfaceAndObject_UseCoordinatesAndHandleNull()
    {
        var point = new Point2D(1, 2);
        IPoint2D same = new Point2D(1, 2);
        IPoint2D? missing = null;
        Assert.True(point == same);
        Assert.False(point != same);
        Assert.True(point.Equals(same));
        Assert.True(point.Equals((object)new Point2D(1, 2)));
        Assert.Equal(point.GetHashCode(), new Point2D(1, 2).GetHashCode());
        Assert.False(point == missing);
        Assert.True(point != missing);
        Assert.False(point.Equals(missing));
        Assert.False(point.Equals("(1, 2)"));
        Assert.Equal(1, point.CompareTo(missing));
    }

    [Fact]
    public void Comparisons_OrderLexicographicallyAndCompareBoundsPerCoordinate()
    {
        var point = new Point2D(1, 5);
        Assert.True(point.CompareTo(new Point2D(2, 0)) < 0);
        Assert.True(point.CompareTo((IPoint2D)new Point2D(1, 4)) > 0);
        Assert.False(point < new Point2D(2, 0));
        Assert.True(point < new Point2D(2, 6));
        Assert.True(point >= new Point2D(1, 5));
        Assert.True(point <= (IPoint2D)new Point2D(1, 6));
        Assert.True(point > (IPoint2D)new Point2D(0, 4));
    }

    [Fact]
    public void Copy_FromThreeDimensions_DropsZAndDoesNotAliasOriginal()
    {
        var source = new Point3D(10, 20, -5);
        var point = new Point2D(source);
        source.X = 99;
        Assert.Equal(new Point2D(10, 20), point);
        Assert.Equal(point, new Point2D((IPoint2D)point));
        Assert.Equal(point, new Point2D(point));
    }
}
