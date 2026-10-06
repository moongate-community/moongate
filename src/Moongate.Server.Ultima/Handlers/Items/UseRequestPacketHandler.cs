using Moongate.Server.Ultima.Interfaces.Items;
using System.Collections.Frozen;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Titles;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     Answers a double click (0x06). On a container the session's character carries, such as its backpack or a bag in
///     it: the container's gump (0x24), then its items (0x3C). On a human-bodied mobile in view range, or on the
///     character's own paperdoll button (the serial with its high bit set): the paperdoll (0x88), with lifting allowed
///     only on the character's own. Anything else is not handled yet.
/// </summary>
/// <remarks>
///     A container is an item whose graphic has the tiledata Container flag; <see cref="IContainerLayoutService" />
///     gives its gump.
/// </remarks>
public sealed class UseRequestPacketHandler : IPacketHandler<UseRequestPacket>
{
    // The client sets this bit on the serial when the player asks for their own paperdoll.
    private const uint PaperdollRequestFlag = 0x80000000;
    private const int TooFarCliloc = 500446;
    private const int DeadCliloc = 1019048;
    private const string UseFunction = "on_use";
    private const string GhostUseFunction = "on_ghost_use";

    private readonly ILogger _logger = Log.ForContext<UseRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly Lazy<FrozenDictionary<int, BodyType>> _bodies;
    private readonly WorldConfig _world;
    private readonly ITileDataService _tiles;
    private readonly IContainerLayoutService _layouts;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;
    private readonly IFameKarmaTitleService _titles;
    private readonly IItemScriptService? _scripts;
    private readonly IBankService? _bank;
    private readonly IInventoryMutationGuard? _inventory;

    public UseRequestPacketHandler(
        IItemService items,
        IMobileService mobiles,
        IDataLoaderService data,
        WorldConfig world,
        ITileDataService tiles,
        IContainerLayoutService layouts,
        IPacketSendService sender,
        ITooltipService tooltips,
        IFameKarmaTitleService titles,
        IItemScriptService? scripts = null,
        IBankService? bank = null,
        IInventoryMutationGuard? inventory = null
    )
    {
        _inventory = inventory;
        _bank = bank;
        _tooltips = tooltips;
        _titles = titles;
        _scripts = scripts;
        _items = items;
        _mobiles = mobiles;
        _bodies = new(() => data.GetEntities<BodyContent>().ToFrozenDictionary(body => (int)body.Body.Value, body => body.Type));
        _world = world;
        _tiles = tiles;
        _layouts = layouts;
        _sender = sender;
    }

    public void Handle(GameSession session, UseRequestPacket packet)
    {
        if ((packet.Target.Value & PaperdollRequestFlag) != 0)
        {
            OpenOwnPaperdoll(session);

            return;
        }

        if (session.CharacterId.IsValid && _mobiles.TryGet(packet.Target, out var mobile))
        {
            OpenPaperdoll(session, mobile);

            return;
        }

        if (!session.CharacterId.IsValid || !_items.TryGet(packet.Target, out var item))
        {
            _logger.Debug("Session {SessionId} used {Target}, which is not a live item", session.SessionId, packet.Target);

            return;
        }

        // What lies in a bank box, and the box itself, is used only while the bank is open.
        if (_bank is not null &&
            _mobiles.TryGet(session.CharacterId, out var user) &&
            !_bank.CanAccess(session, user, item))
        {
            _logger.Debug("Session {SessionId} used {Item}, in a bank that is not open", session.SessionId, item);

            return;
        }

        if (_scripts is not null && _scripts.HasScript(item) && RunOnUse(session, item))
        {
            return;
        }

        if (!_tiles.TryGetItem(item.ItemId, out var tile) || (tile.Flags & TileFlagType.Container) == 0)
        {
            _logger.Debug("Session {SessionId} used {Item}, which is not a container", session.SessionId, item);

            return;
        }

        if (_items.GetOwner(item) != session.CharacterId && !CanOpenOnTheGround(session, item))
        {
            _logger.Debug(
                "Session {SessionId} tried to open {Item}, which its character neither carries nor reaches on the ground",
                session.SessionId,
                item
            );

            return;
        }

        var gump = _layouts.GetLayout(item.ItemId).Gump;
        _sender.TrySend(session.SessionId, new DisplayContainerPacket(item.Id, gump, session.UsesHighSeasContainers()));
        var contents = _items.GetContents(item.Id);
        _sender.TrySend(session.SessionId, new ContainerContentPacket(contents, session.UsesContainerGrid()));

        // As ModernUO and Source-X: each item shown is followed by its tooltip revision.
        foreach (var content in contents)
        {
            _sender.TrySend(session.SessionId, _tooltips.Info(content));
        }
    }

    // A container lying on the ground, or inside one, within reach of the character, such as a treasure chest; one too
    // far says so.
    private bool CanOpenOnTheGround(GameSession session, ItemEntity item)
    {
        if (_items.GetGroundRoot(item) is not { } root ||
            !_items.IsLyingOnGround(root) ||
            !_mobiles.TryGet(session.CharacterId, out var character))
        {
            return false;
        }

        if (_items.CanReach(character, root))
        {
            return true;
        }

        _sender.TrySend(session.SessionId, new LocalizedMessagePacket(item.Id, item.ItemId, TooFarCliloc, "", ""));

        return false;
    }

    // The item's on_use, for an item the character carries or reaches on the ground; true when the script handled the
    // double click, by returning true or by waiting, so the default action must not follow.
    private bool RunOnUse(GameSession session, ItemEntity item)
    {
        if (!_mobiles.TryGet(session.CharacterId, out var character))
        {
            return true;
        }

        if (_inventory?.AllowsOwner(character.Id) == false || _inventory?.Allows(item) == false)
        {
            return true;
        }

        if (_items.GetOwner(item) != character.Id &&
            (_items.GetGroundRoot(item) is not { } root ||
             !_items.IsLyingOnGround(root) ||
             !_items.CanReach(character, root)))
        {
            _sender.TrySend(session.SessionId, new LocalizedMessagePacket(item.Id, item.ItemId, TooFarCliloc, "", ""));

            return true;
        }

        // A ghost uses only what its script says a ghost may, such as an ankh.
        if (character.IsDead)
        {
            var ghost = _scripts!.Run(item, GhostUseFunction, (long)character.Id.Value);

            if (ghost.Kind == ScriptResultKind.Missing)
            {
                _sender.TrySend(session.SessionId, new LocalizedMessagePacket(item.Id, item.ItemId, DeadCliloc, "", ""));
            }

            return true;
        }

        var result = _scripts!.Run(item, UseFunction, (long)character.Id.Value);

        return result.Kind == ScriptResultKind.Suspended ||
               (result.Kind == ScriptResultKind.Completed && result.Values is [true, ..]);
    }

    // As ModernUO's Titles.ComputeTitle: the fame and karma prefix of titles.toml, whose rows from 10000 fame already
    // say Lord or Lady, the name, then the mobile's own title, such as "The Glorious Lady Lilly, the Noble".
    private string PaperdollTitle(MobileEntity mobile)
    {
        var prefix = _titles.GetTitle(mobile);
        var name = string.IsNullOrEmpty(prefix) ? mobile.Name : $"{prefix} {mobile.Name}";

        return string.IsNullOrEmpty(mobile.Title) ? name : $"{name}, {mobile.Title}";
    }

    private void OpenOwnPaperdoll(GameSession session)
    {
        if (session.CharacterId.IsValid && _mobiles.TryGet(session.CharacterId, out var character))
        {
            OpenPaperdoll(session, character);
        }
    }

    // As ModernUO: only human bodies have a paperdoll, and only a mobile the character can see opens it.
    private void OpenPaperdoll(GameSession session, MobileEntity mobile)
    {
        var own = mobile.Id == session.CharacterId;

        if (!own)
        {
            if (!_mobiles.TryGet(session.CharacterId, out var character) ||
                mobile.IsHiddenFrom(character.Id, session.AccountType) ||
                character.Map != mobile.Map ||
                Math.Abs(character.Location.X - mobile.Location.X) > _world.ViewRange ||
                Math.Abs(character.Location.Y - mobile.Location.Y) > _world.ViewRange)
            {
                _logger.Debug("Session {SessionId} asked for the paperdoll of {Mobile}, out of view", session.SessionId, mobile.Id);

                return;
            }
        }

        if (!_bodies.Value.TryGetValue(mobile.Body, out var type) || type != BodyType.Human)
        {
            _logger.Information("Session {SessionId} asked for the paperdoll of {Mobile}, which has no human body", session.SessionId, mobile.Id);

            return;
        }

        _sender.TrySend(session.SessionId, new DisplayPaperdollPacket(mobile.Id, PaperdollTitle(mobile), mobile.WarMode, own));
    }
}
