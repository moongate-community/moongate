using System.Globalization;
using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Rectangle2DTests
{
    [Theory,
     InlineData(10, 20, true),
     InlineData(13, 24, true),
     InlineData(14, 20, false),
     InlineData(10, 25, false),
     InlineData(9, 20, false)]
    public void Contains_UsesInclusiveStartAndExclusiveEnd(int x, int y, bool expected)
    {
        var rectangle = new Rectangle2D(10, 20, 4, 5);
        Assert.Equal(expected, rectangle.Contains(x, y));
        Assert.Equal(expected, rectangle.Contains(new Point2D(x, y)));
        Assert.Equal(expected, rectangle.Contains(new Point3D(x, y, 100)));
    }

    [Theory, InlineData("(10, 20)+(4, 5)"), InlineData(" (+10, +20) + (+4, +5) ")]
    public void Parse_PositionAndSize_ProducesExpectedBounds(string text)
    {
        var expected = new Rectangle2D(new Point2D(10, 20), new Point2D(14, 25));
        Assert.Equal(expected, Rectangle2D.Parse(text));
        Assert.True(Rectangle2D.TryParse(text, null, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(expected, Rectangle2D.Parse(expected.ToString()));
    }

    [Theory,
     InlineData(null),
     InlineData(""),
     InlineData("(1,2)"),
     InlineData("(1,2)+(x,4)"),
     InlineData("(1,2)+(3,4)+(5,6)")]
    public void TryParse_InvalidBounds_ReturnsFalseAndDefault(string? text)
    {
        Assert.False(Rectangle2D.TryParse(text, null, out var result));
        Assert.Equal(Rectangle2D.Empty, result);
        Assert.Throws<FormatException>(() => Rectangle2D.Parse(text!));
    }

    [Fact]
    public void MakeHold_ExpandsBothBoundsAndPreservesExistingContents()
    {
        var rectangle = new Rectangle2D(10, 20, 4, 5);
        rectangle.MakeHold(new Rectangle2D(8, 22, 10, 10));
        Assert.Equal(new Point2D(8, 20), rectangle.Start);
        Assert.Equal(new Point2D(18, 32), rectangle.End);
        Assert.True(rectangle.Contains(10, 20));
    }

    [Fact]
    public void SetAndResize_UpdateBoundsAndValueEquality()
    {
        var rectangle = Rectangle2D.Empty;
        Assert.False(rectangle.Contains(Point2D.Zero));
        rectangle.Set(10, 20, 4, 5);
        rectangle.Width = 6;
        rectangle.Height = 7;
        var expected = new Rectangle2D(10, 20, 6, 7);
        Assert.True(rectangle == expected);
        Assert.False(rectangle != expected);
        Assert.True(rectangle.Equals((object)expected));
        Assert.Equal(expected.GetHashCode(), rectangle.GetHashCode());
        Assert.Equal(new Point2D(16, 27), rectangle.End);
    }

    [Fact]
    public void Formatting_UsesProviderAndRejectsSmallDestination()
    {
        var rectangle = new Rectangle2D(-1, 2, 3, 4);
        var provider = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        provider.NegativeSign = "minus";
        Assert.Equal("(minus1, 2)+(3, 4)", rectangle.ToString(null, provider));
        Span<char> destination = stackalloc char[32];
        Assert.True(rectangle.TryFormat(destination, out var written, default, provider));
        Assert.Equal("(minus1, 2)+(3, 4)", destination[..written].ToString());
        Assert.False(rectangle.TryFormat(destination[..2], out written, default, provider));
        Assert.Equal(0, written);
    }
}
