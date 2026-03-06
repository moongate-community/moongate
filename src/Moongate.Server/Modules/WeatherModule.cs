using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Spatial;

namespace Moongate.Server.Modules;

[ScriptModule("weather", "Provides weather and global light controls for scripts.")]

/// <summary>
/// Exposes weather/light helpers to Lua scripts.
/// </summary>
public sealed class WeatherModule
{
    private readonly ILightService _lightService;

    public WeatherModule(ILightService lightService)
    {
        _lightService = lightService;
    }

    [ScriptFunction("set_global_light", "Forces global light level for all players (0-255).")]
    public void SetGlobalLight(int level)
    {
        _lightService.SetGlobalLightOverride(level, true);
    }

    [ScriptFunction("clear_global_light", "Clears forced global light level and restores dynamic cycle.")]
    public void ClearGlobalLight()
    {
        _lightService.SetGlobalLightOverride(null, true);
    }
}
