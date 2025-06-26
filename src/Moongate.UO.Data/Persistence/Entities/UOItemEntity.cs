using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOItemEntity
{
    public Serial Id { get; set; }
    public int ItemId { get; set; }
    public string Name { get; set; }
    public int Gold { get; set; }
    public int Weight { get; set; }
    public int Hue { get; set; }

    public ItemReference ToItemReference()
    {
        return new ItemReference(Id, ItemId, Hue);
    }

    public static explicit operator ItemReference(UOItemEntity item)
    {
        return item.ToItemReference();
    }
}
