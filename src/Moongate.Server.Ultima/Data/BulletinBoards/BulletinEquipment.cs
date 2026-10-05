namespace Moongate.Server.Ultima.Data.BulletinBoards;

/// <summary>
///     One piece of what the poster of a bulletin board message wore: the client draws the poster with it.
/// </summary>
public readonly record struct BulletinEquipment
{
    public int ItemId { get; }

    public int Hue { get; }

    public BulletinEquipment(int itemId, int hue)
    {
        ItemId = itemId;
        Hue = hue;
    }
}
