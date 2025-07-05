using System.ComponentModel;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOItemEntity : IPositionEntity, ISerialEntity, INotifyPropertyChanged
{
    public delegate void ContainerItemChangedEventHandler(UOItemEntity container, ItemReference item);

    public delegate void ItemMovedEventHandler(UOItemEntity item, Point3D oldLocation, Point3D newLocation);



    public event ItemMovedEventHandler? ItemMoved;

    public event ContainerItemChangedEventHandler? ContainerItemAdded;
    public event ContainerItemChangedEventHandler? ContainerItemRemoved;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string TemplateId { get; set; }
    public int Amount { get; set; } = 1;
    public Serial Id { get; set; }
    public int ItemId { get; set; }
    public string Name { get; set; }
    public int Gold { get; set; }
    public double Weight => BaseWeight * Amount;
    public int Hue { get; set; }
    public Serial OwnerId { get; set; }
    public Serial? ParentId { get; set; }
    public DecayType Decay { get; set; } = DecayType.ItemDecay;
    public int? GumpId { get; set; }
    public string ScriptId { get; set; }
    public bool IsStackable { get; set; }
    public int BaseWeight { get; set; }

    public bool IsContainer => GumpId.HasValue;
    public bool IsOnGround => ParentId == null || Location == new Point3D(-1, -1, -1);
    public Point3D Location { get; private set; } = new Point3D(-1, -1, -1);
    public void MoveTo(Point3D newLocation)
    {
        var oldLocation = Location;
        Location = newLocation;
        ItemMoved?.Invoke(this, oldLocation, newLocation);
    }

    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

    public bool CanDecay => Decay != DecayType.None;
    public Dictionary<Point2D, ItemReference> ContainedItems { get; set; } = new();

    public void AddItem(UOItemEntity item, Point2D position)
    {
        item.ParentId = Id;

        item.Location = new Point3D(position.X, position.Y, -1); // Assuming Z is the same as the container's Z

        ContainedItems[position] = item.ToItemReference();

        ContainerItemAdded?.Invoke(this, item.ToItemReference());
    }

    public bool ContainsItem(UOItemEntity item)
    {
        return ContainedItems.Values.ToList().FirstOrDefault(s => s.Id == item.Id) != null;
    }

    public void RemoveItem(UOItemEntity item)
    {
        // Logic to remove an item from this item, e.g., from a container
        if (item.ParentId == Id)
        {
            ContainedItems.Remove(new Point2D(item.Location.X, item.Location.Y));
            ContainerItemRemoved?.Invoke(this, item.ToItemReference());
            item.ParentId = null;
        }
    }

    public void RemoveItemFromBackpack(Point2D position)
    {
        // Logic to remove an item from this item, e.g., from a container
        ContainedItems.Remove(position);
        ContainerItemRemoved?.Invoke(this, new ItemReference(Id, ItemId, Hue));
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
