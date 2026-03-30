using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Modules;

[ScriptModule("magic", "Provides spell casting state helpers for Lua brains.")]
public sealed class MagicModule
{
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IMagicService _magicService;
    private readonly IItemService? _itemService;

    public MagicModule(
        ISpatialWorldService spatialWorldService,
        IMagicService magicService,
        IItemService? itemService = null
    )
    {
        ArgumentNullException.ThrowIfNull(spatialWorldService);
        ArgumentNullException.ThrowIfNull(magicService);

        _spatialWorldService = spatialWorldService;
        _magicService = magicService;
        _itemService = itemService;
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

    [ScriptFunction("cast", "Starts a spell cast for the resolved npc without binding a target.")]
    public bool Cast(uint npcSerial, int spellId)
    {
        if (!TryResolveCaster(npcSerial, out var npc))
        {
            return false;
        }

        return _magicService.TryCastAsync(npc!, spellId).AsTask().GetAwaiter().GetResult();
    }

    [ScriptFunction("cast_mobile", "Starts a spell cast and binds a mobile target when both mobiles resolve.")]
    public bool CastMobile(uint npcSerial, int spellId, uint targetSerial)
    {
        if (targetSerial == 0 ||
            !TryResolveCaster(npcSerial, out var npc) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out var target))
        {
            return false;
        }

        return TryCastWithTarget(npc!, spellId, SpellTargetData.Mobile(target!.Id));
    }

    [ScriptFunction("cast_item", "Starts a spell cast and binds an item target when the item resolves.")]
    public bool CastItem(uint npcSerial, int spellId, uint itemSerial)
    {
        if (itemSerial == 0 || _itemService is null || !TryResolveCaster(npcSerial, out var npc))
        {
            return false;
        }

        var item = _itemService.GetItemAsync((Moongate.UO.Data.Ids.Serial)itemSerial).GetAwaiter().GetResult();

        if (item is null)
        {
            return false;
        }

        return TryCastWithTarget(npc!, spellId, SpellTargetData.Item(item.Id, item.Location, (ushort)item.ItemId));
    }

    [ScriptFunction("cast_location", "Starts a spell cast and binds a world location target.")]
    public bool CastLocation(uint npcSerial, int spellId, int mapId, int x, int y, int z)
    {
        if (!TryResolveCaster(npcSerial, out var npc))
        {
            return false;
        }

        return TryCastWithTarget(npc!, spellId, SpellTargetData.FromLocation(mapId, new Point3D(x, y, z)));
    }

    private bool TryCastWithTarget(Moongate.UO.Data.Persistence.Entities.UOMobileEntity caster, int spellId, SpellTargetData target)
    {
        var castSucceeded = _magicService.TryCastAsync(caster, spellId).AsTask().GetAwaiter().GetResult();

        if (!castSucceeded)
        {
            return false;
        }

        var targetSucceeded = _magicService.TrySetTargetAsync(caster.Id, spellId, target)
                                          .AsTask()
                                          .GetAwaiter()
                                          .GetResult();

        if (targetSucceeded)
        {
            return true;
        }

        _magicService.Interrupt(caster.Id);

        return false;
    }

    private bool TryResolveCaster(uint npcSerial, out Moongate.UO.Data.Persistence.Entities.UOMobileEntity? npc)
        => MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out npc);
}
