using System.Globalization;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Geometry;
using Moongate.Core.Types.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Point3DTests
{
    [Fact]
    public void CopyAndArithmetic_PreserveAllCoordinates()
    {
        var point = new Point3D(new(10, 20), -5);
        Assert.Equal(point, new((IPoint3D)point));
        Assert.Equal(point, new(point));
        Assert.Equal(new(11, 22, -2), point + new Point3D(1, 2, 3));
        Assert.Equal(new(9, 18, -8), point - new Point3D(1, 2, 3));
    }

    [Fact]
    public void DistanceAndDirection_ExtremeCoordinates_DoNotOverflow()
    {
        var left = new Point3D(int.MinValue, 0, 0);
        var right = new Point3D(int.MaxValue, 0, 0);
        Assert.Equal(4294967295d, left.GetDistance(right));
        Assert.Equal(4294967295d, left.GetDistance3D((IPoint3D)right));
        Assert.False(left.InRange(right, int.MaxValue));
        Assert.False(left.InRange3D((IPoint3D)right, int.MaxValue));
        Assert.Equal(DirectionType.East, left.GetDirectionTo(right));
        Assert.Equal(DirectionType.West, right.GetDirectionTo(left));
        Assert.Equal(50000d, Point3D.Zero.GetDistance(new(30000, 40000, 0)));
    }

    [Fact]
    public void DistanceAndRange_SeparatePlanarAndThreeDimensionalDistance()
    {
        var target = new Point3D(3, 4, 12);
        var origin = Point3D.Zero;
        Assert.Equal(5, origin.GetDistance(target));
        Assert.Equal(13, origin.GetDistance3D(target));
        Assert.Equal(5, origin.GetDistance((IPoint3D)target));
        Assert.Equal(13, origin.GetDistance3D((IPoint3D)target));
        Assert.True(origin.InRange(target, 5));
        Assert.False(origin.InRange(target, 4));
        Assert.True(origin.InRange3D(target, 13));
        Assert.False(origin.InRange3D(target, 12));
        Assert.True(origin.InRange((IPoint3D)target, 5));
        Assert.True(origin.InRange3D((IPoint3D)target, 13));
        Assert.False(origin.InRange(origin, -1));
        Assert.False(origin.InRange3D((IPoint3D)origin, -1));
        Assert.False(origin.InRange(null, 10));
        Assert.False(origin.InRange3D(null, 10));
        Assert.Equal(double.MaxValue, origin.GetDistance(null));
        Assert.Equal(double.MaxValue, origin.GetDistance3D(null));
    }

    [Fact]
    public void EqualityAndOrdering_HandleInterfacesAndNull()
    {
        var point = new Point3D(1, 2, 3);
        IPoint3D same = new Point3D(1, 2, 3);
        IPoint3D? missing = null;
        Assert.True(point == same);
        Assert.False(point != same);
        Assert.True(point.Equals(same));
        Assert.True(point.Equals((object)new Point3D(1, 2, 3)));
        Assert.Equal(point.GetHashCode(), new Point3D(1, 2, 3).GetHashCode());
        Assert.False(point == missing);
        Assert.True(point != missing);
        Assert.False(point.Equals(missing));
        Assert.Equal(1, point.CompareTo(missing));
        Assert.True(point.CompareTo(new(2, 0, 0)) < 0);
        Assert.True(point.CompareTo((IPoint3D)new Point3D(1, 2, 2)) > 0);
        Assert.True(point >= new Point3D(1, 2, 3));
        Assert.True(point < (IPoint3D)new Point3D(2, 3, 4));
        Assert.False(point < new Point3D(2, 1, 4));
    }

    [Fact]
    public void Formatting_UsesProviderAndRejectsSmallDestination()
    {
        var provider = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        provider.NegativeSign = "minus";
        var point = new Point3D(-1, 2, 3);
        Assert.Equal("(-1, 2, 3)", point.ToString());
        Assert.Equal("(minus1, 2, 3)", point.ToString(null, provider));
        Span<char> destination = stackalloc char[14];
        Assert.True(point.TryFormat(destination, out var written, default, provider));
        Assert.Equal("(minus1, 2, 3)", destination[..written].ToString());
        Assert.False(point.TryFormat(destination[..3], out written, default, provider));
        Assert.Equal(0, written);
    }

    [Theory,
     InlineData(DirectionType.North, 0, -1),
     InlineData(DirectionType.NorthEast, 1, -1),
     InlineData(DirectionType.East, 1, 0),
     InlineData(DirectionType.SouthEast, 1, 1),
     InlineData(DirectionType.South, 0, 1),
     InlineData(DirectionType.SouthWest, -1, 1),
     InlineData(DirectionType.West, -1, 0),
     InlineData(DirectionType.NorthWest, -1, -1)]
    public void Movement_AllDirections_IgnoreRunningAndPreserveElevation(DirectionType direction, int x, int y)
    {
        var origin = new Point3D(10, 20, -5);
        var running = Point3D.SetRunning(direction);
        Point3D offset = running;
        Assert.Equal(new(x, y, 0), offset);
        Assert.Equal(direction, (DirectionType)offset);
        Assert.Equal(new(10 + x, 20 + y, -5), origin.Move(running));
        Assert.Equal(origin.Move(direction), origin + running);
        Assert.Equal(origin.Move(direction), direction + origin);
        Assert.Equal(origin, origin + direction - running);
        Assert.True(Point3D.IsRunning(running));
        Assert.False(Point3D.IsRunning(direction));
        Assert.Equal(direction, Point3D.GetBaseDirection(running));
    }

    [Theory,
     InlineData("(12, -34, 5)", 12, -34, 5),
     InlineData("  ( +12 , -34 , +5 )  ", 12, -34, 5),
     InlineData("(-2147483648, 0, 2147483647)", int.MinValue, 0, int.MaxValue)]
    public void Parse_ValidCoordinates_ReadsStringAndSpan(string text, int x, int y, int z)
    {
        var expected = new Point3D(x, y, z);
        Assert.Equal(expected, Point3D.Parse(text));
        Assert.Equal(expected, Point3D.Parse(text.AsSpan(), CultureInfo.InvariantCulture));
        Assert.True(Point3D.TryParse(text, null, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void Range_ExtremeBoundary_DoesNotRoundAnOutsidePointIntoRange()
    {
        var boundary = new Point3D(int.MaxValue, 0, 0);
        var outside = new Point3D(int.MaxValue, 1, 0);
        Assert.True(Point3D.Zero.InRange(boundary, int.MaxValue));
        Assert.True(Point3D.Zero.InRange3D((IPoint3D)boundary, int.MaxValue));
        Assert.False(Point3D.Zero.InRange(outside, int.MaxValue));
        Assert.False(Point3D.Zero.InRange3D((IPoint3D)outside, int.MaxValue));
    }

    [Theory,
     InlineData(null),
     InlineData(""),
     InlineData("()"),
     InlineData("1,2,3"),
     InlineData("(1,2)"),
     InlineData("(1,2,3,4)"),
     InlineData("(1,,3)"),
     InlineData("(0,0,2147483648)"),
     InlineData("(1,2,x)")]
    public void TryParse_InvalidCoordinates_ReturnsFalseAndDefault(string? text)
    {
        Assert.False(Point3D.TryParse(text, null, out var point));
        Assert.Equal(Point3D.Zero, point);
        Assert.Throws<FormatException>(() => Point3D.Parse(text!));
    }
}
