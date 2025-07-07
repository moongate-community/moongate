using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Containers;

/// <summary>
/// Helper class for tracking occupied space in a container
/// </summary>
public class ContainerLayout
{
    private readonly bool[,] _occupiedSlots;
    private readonly ContainerSize _containerSize;

    public ContainerLayout(ContainerSize containerSize)
    {
        _containerSize = containerSize;
        _occupiedSlots = new bool[containerSize.Width, containerSize.Height];
    }

    /// <summary>
    /// Finds the next available position for an item
    /// </summary>
    public Point2D? FindNextAvailablePosition(UOItemEntity item)
    {
        var itemSize = ContainerLayoutSystem.GetItemSize(item);

        for (int y = 0; y <= _containerSize.Height - itemSize.Height; y++)
        {
            for (int x = 0; x <= _containerSize.Width - itemSize.Width; x++)
            {
                if (CanPlaceItemAt(x, y, itemSize))
                {
                    return new Point2D(x, y);
                }
            }
        }

        return null; /// No space available
    }

    /// <summary>
    /// Checks if an item can be placed at the specified position
    /// </summary>
    private bool CanPlaceItemAt(int x, int y, Rectangle2D itemSize)
    {
        for (int checkX = x; checkX < x + itemSize.Width; checkX++)
        {
            for (int checkY = y; checkY < y + itemSize.Height; checkY++)
            {
                if (checkX >= _containerSize.Width || checkY >= _containerSize.Height)
                    return false;

                if (_occupiedSlots[checkX, checkY])
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Marks space as occupied by an item
    /// </summary>
    public void MarkSpaceOccupied(Point2D position, Rectangle2D itemSize)
    {
        for (int x = position.X; x < position.X + itemSize.Width; x++)
        {
            for (int y = position.Y; y < position.Y + itemSize.Height; y++)
            {
                if (x < _containerSize.Width && y < _containerSize.Height)
                {
                    _occupiedSlots[x, y] = true;
                }
            }
        }
    }
}
