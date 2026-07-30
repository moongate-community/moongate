using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Ultima.Types;
using MoonSharp.Interpreter;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Scripting.Refs;

/// <summary>
/// Builds the handle <c>mobile.ref</c> hands to Lua: a table of closures over a serial, each
/// delegating to the service that owns the operation.
/// <para>
/// The handle keeps a serial and never an entity, so it cannot go stale — every call re-reads, and a
/// mobile that died between two calls is an ordinary false. Closures rather than MoonSharp userdata
/// because userdata exposes members under their CLR names, which would put a capital in the middle
/// of a snake_case surface; taking no <c>self</c> is also why Lua calls these with a dot.
/// </para>
/// </summary>
public sealed class MobileRefFactory
{
    private readonly Script _script;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IChatService _chat;
    private readonly IMobileService _mobileService;
    private readonly IItemService _items;

    public MobileRefFactory(
        Script script,
        IPersistenceService persistenceService,
        IChatService chat,
        IMobileService mobileService,
        IItemService items
    )
    {
        _script = script;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _chat = chat;
        _mobileService = mobileService;
        _items = items;
    }

    /// <summary>
    /// A handle for the mobile, or <see cref="DynValue.Nil" /> when the serial names none. The nil is
    /// an early signal only: the handle re-reads on every call, so it may still find nothing later.
    /// </summary>
    public DynValue Create(Serial mobile)
    {
        if (_mobiles.GetById(mobile) is null)
        {
            return DynValue.Nil;
        }

        var table = new Table(_script)
        {
            ["say"] = (Func<string, bool>)(text => Say(mobile, text)),
            ["teleport"] = (Func<int, int, int, bool>)((x, y, z) => _mobileService.Teleport(mobile, x, y, z)),
            ["equip"] = (Func<uint, object, bool>)((item, layer) => Equip(mobile, item, layer))
        };

        return DynValue.NewTable(table);
    }

    private bool Equip(Serial mobile, uint item, object layer)
    {
        if (!ScriptEnums.TryResolve<LayerType>(layer, out var parsed))
        {
            return false;
        }

        if (_mobiles.GetById(mobile) is not { } owner || _items.GetById((Serial)item) is not { } entity)
        {
            return false;
        }

        _items.Equip(owner, entity, parsed);

        return true;
    }

    private bool Say(Serial mobile, string text)
        => _mobiles.GetById(mobile) is { } speaker && _chat.SayAs(speaker, text);
}
