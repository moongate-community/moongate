namespace Moongate.Core.Geometry;

/// <summary>
///     Represents Point3DList.
/// </summary>
public class Point3DList
{
    private const int InitialCapacity = 16;

    private static readonly Point3D[] _emptyList = [];

    private Point3D[] _list;

    public int Count { get; private set; }

    public Point3D Last
        => Count > 0
            ? _list[Count - 1]
            : throw new InvalidOperationException("The point list is empty.");

    public Point3D this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

            return _list[index];
        }
    }

    public Point3DList()
    {
        _list = new Point3D[InitialCapacity];
        Count = 0;
    }

    public void Add(int x, int y, int z)
    {
        EnsureCapacity(Count + 1);

        _list[Count].X = x;
        _list[Count].Y = y;
        _list[Count].Z = z;
        ++Count;
    }

    public void Add(Point3D p)
    {
        EnsureCapacity(Count + 1);

        _list[Count].X = p.X;
        _list[Count].Y = p.Y;
        _list[Count].Z = p.Z;
        ++Count;
    }

    public void Clear()
    {
        Count = 0;
    }

    /// <summary>
    ///     Copies the accumulated points into an independent array and clears this list.
    /// </summary>
    public Point3D[] ToArray()
    {
        if (Count == 0)
        {
            return _emptyList;
        }

        var list = new Point3D[Count];

        for (var i = 0; i < Count; ++i)
        {
            list[i] = _list[i];
        }

        Count = 0;

        return list;
    }

    private void EnsureCapacity(int requiredCount)
    {
        if (requiredCount <= _list.Length)
        {
            return;
        }

        var newSize = _list.Length * 2;

        while (newSize < requiredCount)
        {
            newSize *= 2;
        }

        var old = _list;
        _list = new Point3D[newSize];
        Array.Copy(old, _list, old.Length);
    }
}
