using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Items;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Modules;

[ScriptModule("door", "Provides server-side helpers to resolve and toggle world doors.")]
public sealed class DoorModule
{
    private readonly IDoorService _doorService;

    public DoorModule(IDoorService doorService)
    {
        _doorService = doorService;
    }

    [ScriptFunction("is_door", "Checks whether the given item serial is a supported door.")]
    public bool IsDoor(uint itemSerial)
    {
        if (itemSerial == 0)
        {
            return false;
        }

        return _doorService.IsDoorAsync((Serial)itemSerial).GetAwaiter().GetResult();
    }

    [ScriptFunction("toggle", "Toggles a supported door open/close.")]
    public bool Toggle(uint itemSerial)
    {
        if (itemSerial == 0)
        {
            return false;
        }

        return _doorService.ToggleAsync((Serial)itemSerial).GetAwaiter().GetResult();
    }
}
