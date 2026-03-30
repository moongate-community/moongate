using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;

namespace Moongate.Server.Modules;

[ScriptModule("magic", "Provides spell casting state helpers for Lua brains.")]
public sealed class MagicModule
{
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IMagicService _magicService;

    public MagicModule(ISpatialWorldService spatialWorldService, IMagicService magicService)
    {
        ArgumentNullException.ThrowIfNull(spatialWorldService);
        ArgumentNullException.ThrowIfNull(magicService);

        _spatialWorldService = spatialWorldService;
        _magicService = magicService;
    }

    [ScriptFunction("is_casting", "Returns whether the resolved npc is currently casting a spell.")]
    public bool IsCasting(uint npcSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return false;
        }

        return _magicService.IsCasting(npc!.Id);
    }

    [ScriptFunction("interrupt", "Interrupts an active spell cast for the resolved npc.")]
    public bool Interrupt(uint npcSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return false;
        }

        _magicService.Interrupt(npc!.Id);

        return true;
    }
}
