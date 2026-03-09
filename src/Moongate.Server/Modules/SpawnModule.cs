using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Modules;

[ScriptModule("spawn", "Provides runtime helpers for world spawner items.")]
public sealed class SpawnModule
{
    private readonly ISpawnService _spawnService;

    public SpawnModule(ISpawnService spawnService)
    {
        _spawnService = spawnService;
    }

    [ScriptFunction("activate", "Forces a spawn attempt for the given spawner item serial.")]
    public bool Activate(uint spawnerItemSerial)
    {
        if (spawnerItemSerial == 0)
        {
            return false;
        }

        return _spawnService.TriggerAsync((Serial)spawnerItemSerial).GetAwaiter().GetResult();
    }
}
