using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Modules;

[ScriptModule("healing", "Provides self-bandage helpers for Lua brains.")]

/// <summary>
/// Exposes minimal self-bandage helpers to Lua brains.
/// </summary>
public sealed class HealingModule
{
    private readonly IBandageService _bandageService;

    public HealingModule(IBandageService bandageService)
    {
        _bandageService = bandageService;
    }

    [ScriptFunction("begin_self_bandage", "Attempts to start a delayed self-bandage on the npc.")]
    public bool BeginSelfBandage(uint npcSerial)
        => _bandageService.BeginSelfBandageAsync((Serial)npcSerial).GetAwaiter().GetResult();

    [ScriptFunction("has_bandage", "Returns true when the npc has at least one bandage in its backpack.")]
    public bool HasBandage(uint npcSerial)
        => _bandageService.HasBandageAsync((Serial)npcSerial).GetAwaiter().GetResult();

    [ScriptFunction("is_bandaging", "Returns true when the npc currently has an in-flight self-bandage.")]
    public bool IsBandaging(uint npcSerial)
        => _bandageService.IsBandaging((Serial)npcSerial);
}
