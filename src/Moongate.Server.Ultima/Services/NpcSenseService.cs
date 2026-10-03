using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Queues <c>on_mobile_in_range(serial, other)</c> both ways: an NPC senses a mobile, player or NPC, that comes within
///     the sense range, and a moving NPC senses the mobiles it comes near. Queued, not run: a step may come from inside a
///     running script.
/// </summary>
public sealed class NpcSenseService : INpcSenseService
{
    public const string Function = "on_mobile_in_range";

    private readonly INpcScriptService _scripts;
    private readonly ISectorService _sectors;
    private readonly NpcsConfig _config;

    public NpcSenseService(INpcScriptService scripts, ISectorService sectors, NpcsConfig config)
    {
        _scripts = scripts;
        _sectors = sectors;
        _config = config;
    }

    public void Appeared(MobileEntity mobile)
    {
        Sense(mobile, null);
    }

    public void Moved(MobileEntity mobile, Point3D oldLocation)
    {
        if (oldLocation != mobile.Location)
        {
            Sense(mobile, oldLocation);
        }
    }

    // A square along X and Y, as the sectors and the view range measure it.
    private static bool IsWithin(Point3D point, Point3D center, int range)
    {
        return Math.Abs(point.X - center.X) <= range && Math.Abs(point.Y - center.Y) <= range;
    }

    // Everyone now within range of the mobile who was not within range of its old position, if it had one.
    private void Sense(MobileEntity mobile, Point3D? oldLocation)
    {
        var range = _config.SenseRange;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, range))
        {
            if (other.Id == mobile.Id || (oldLocation is { } old && IsWithin(old, other.Location, range)))
            {
                continue;
            }

            // A hidden mobile is sensed by no one.
            if (other.IsNpc && !mobile.Hidden)
            {
                _scripts.Queue(other, Function, (long)mobile.Id.Value);
            }

            if (mobile.IsNpc && !other.Hidden)
            {
                _scripts.Queue(mobile, Function, (long)other.Id.Value);
            }
        }
    }
}
