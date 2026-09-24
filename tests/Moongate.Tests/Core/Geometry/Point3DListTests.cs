using Moongate.Core.Geometry;

namespace Moongate.Tests.Core.Geometry;

public class Point3DListTests
{
    [Fact]
    public void Add_BeyondInitialCapacity_PreservesOrderAndLastPoint()
    {
        var list = new Point3DList();

        for (var i = 0; i < 40; i++)
        {
            list.Add(i, -i, 5);
        }

        list.Add(new(100, 200, -5));
        Assert.Equal(41, list.Count);
        Assert.Equal(new(0, 0, 5), list[0]);
        Assert.Equal(new(39, -39, 5), list[39]);
        Assert.Equal(new(100, 200, -5), list.Last);
    }

    [Fact]
    public void ClearAndIndexing_DoNotExposeUnusedOrRemovedPoints()
    {
        var list = new Point3DList();
        Assert.Throws<InvalidOperationException>(() => list.Last);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[0]);
        list.Add(1, 2, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[1]);
        list.Clear();
        Assert.Equal(0, list.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[0]);
        Assert.Throws<InvalidOperationException>(() => list.Last);
    }

    [Fact]
    public void ToArray_ReturnsIndependentPointsAndDrainsList()
    {
        var list = new Point3DList();
        list.Add(1, 2, 3);
        list.Add(4, 5, 6);
        var points = list.ToArray();
        Assert.Equal(new[] { new Point3D(1, 2, 3), new Point3D(4, 5, 6) }, points);
        Assert.Equal(0, list.Count);
        Assert.Empty(list.ToArray());
        list.Add(7, 8, 9);
        Assert.Equal(new(1, 2, 3), points[0]);
        Assert.Equal(new(7, 8, 9), list.Last);
    }
}
