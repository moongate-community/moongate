using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOItemEntity
{
    public string TemplateId { get; set; }
    public Serial Id { get; set; }
    public int ItemId { get; set; }
    public string Name { get; set; }
    public int Gold { get; set; }
    public double Weight { get; set; }
    public int Hue { get; set; }
    public Serial? ParentId { get; set; }
    public DecayType Decay { get; set; } = DecayType.ItemDecay;

    public int? GumpId { get; set; }

    public bool IsContainer => GumpId.HasValue;

    public Dictionary<Point2D, ItemReference> ContainedItems { get; set; } = new();

    public void AddItem(UOItemEntity item, Point2D position)
    {
        // Logic to add an item to this item, e.g., in a container
        // This could involve updating the ParentId of the item being added
        item.ParentId = Id;

        ContainedItems[position] = item.ToItemReference();
    }

    public void RemoveItem(Point2D position)
    {
        // Logic to remove an item from this item, e.g., from a container
        ContainedItems.Remove(position);
    }

    public ItemReference ToItemReference()
    {
        return new ItemReference(Id, ItemId, Hue);
    }

    public static explicit operator ItemReference(UOItemEntity item)
    {
        return item.ToItemReference();
    }
}
