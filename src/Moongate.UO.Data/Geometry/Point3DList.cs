namespace Moongate.UO.Data.Geometry;

/// <summary>
/// Represents Point3DList.
/// </summary>
public class Point3DList
{
    private static readonly Point3D[] m_EmptyList = [];
    private Point3D[] m_List;

    public Point3DList()
    {
        m_List = new Point3D[16];
        Count = 0;
    }

    public int Count { get; private set; }

    public Point3D Last => m_List[Count - 1];

    public Point3D this[int index] => m_List[index];

    public void Add(int x, int y, int z)
    {
        EnsureCapacity(Count + 1);

        m_List[Count].X = x;
        m_List[Count].Y = y;
        m_List[Count].Z = z;
        ++Count;
    }

    public void Add(Point3D p)
    {
        EnsureCapacity(Count + 1);

        m_List[Count].X = p.X;
        m_List[Count].Y = p.Y;
        m_List[Count].Z = p.Z;
        ++Count;
    }

    public void Clear()
        => Count = 0;

    public Point3D[] ToArray()
    {
        if (Count == 0)
        {
            return m_EmptyList;
        }

        var list = new Point3D[Count];

        for (var i = 0; i < Count; ++i)
        {
            list[i] = m_List[i];
        }

        Count = 0;

        return list;
    }

    private void EnsureCapacity(int requiredCount)
    {
        if (requiredCount <= m_List.Length)
        {
            return;
        }

        var newSize = m_List.Length * 2;

        while (newSize < requiredCount)
        {
            newSize *= 2;
        }

        var old = m_List;
        m_List = new Point3D[newSize];
        Array.Copy(old, m_List, old.Length);
    }
}
