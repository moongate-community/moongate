using Moongate.Core.Geometry;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Gives every item a script that moves the mobile to the destination on every run, as a teleporter's does, and
///     counts the runs.
/// </summary>
public sealed class MovingItemScriptService : IItemScriptService
{
    private readonly MobileEntity _mobile;
    private readonly Point3D _destination;

    public int Runs { get; private set; }

    public MovingItemScriptService(MobileEntity mobile, Point3D destination)
    {
        _mobile = mobile;
        _destination = destination;
    }

    public bool HasScript(ItemEntity item)
    {
        return true;
    }

    public ScriptResult Run(ItemEntity item, string function, params object?[] args)
    {
        Runs++;
        _mobile.Location = _destination;

        return ScriptResult.Completed([]);
    }

    public void Queue(ItemEntity item, string function, params object?[] args)
    {
    }
}
