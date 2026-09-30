namespace Moongate.Server.Ultima.Data.Templates.Spawns;

/// <summary>
///     A rectangle of a spawn, both corners included as in UOX3.
/// </summary>
public class SpawnArea
{
    public int X1 { get; set; }

    public int Y1 { get; set; }

    public int X2 { get; set; }

    public int Y2 { get; set; }

    public bool Contains(int x, int y)
    {
        return x >= X1 && x <= X2 && y >= Y1 && y <= Y2;
    }
}
