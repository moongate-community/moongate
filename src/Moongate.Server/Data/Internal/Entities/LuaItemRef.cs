using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Internal.Entities;

/// <summary>
/// Lua-safe item reference wrapper.
/// </summary>
public sealed class LuaItemRef
{
    private readonly UOItemEntity _item;

    public LuaItemRef(UOItemEntity item)
    {
        _item = item;
    }

    public uint Serial => (uint)_item.Id;

    public string Name => _item.Name ?? string.Empty;

    public int MapId => _item.MapId;

    public int LocationX => _item.Location.X;

    public int LocationY => _item.Location.Y;

    public int LocationZ => _item.Location.Z;

    public int Amount => _item.Amount;

    public int ItemId => _item.ItemId;

    public int Hue => _item.Hue;
}
