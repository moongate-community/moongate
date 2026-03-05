using Moongate.Server.Data.World;

namespace Moongate.Server.Data.Internal.World;

/// <summary>
/// Lua-facing proxy for a world location entry.
/// </summary>
public sealed class LuaLocationProxy
{
    private readonly WorldLocationEntry _entry;

    public LuaLocationProxy(WorldLocationEntry entry)
    {
        _entry = entry;
    }

    public int MapId => _entry.MapId;

    public string MapName => _entry.MapName;

    public string CategoryPath => _entry.CategoryPath;

    public string Name => _entry.Name;

    public int LocationX => _entry.Location.X;

    public int LocationY => _entry.Location.Y;

    public int LocationZ => _entry.Location.Z;
}
