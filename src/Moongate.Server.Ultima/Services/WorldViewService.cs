using System.Globalization;
using Moongate.Core.Geometry;
using Moongate.Core.Utils;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Data.Internal.World;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Sends the players in range of a mobile what changed about it, keeping the character → session map itself because
///     <c>ISessionService.TryGetByCharacterId</c> scans every session.
/// </summary>
public sealed class WorldViewService : IWorldViewService
{

    private const int NoDrawGraphic = 0x21A4;
    private const int StaffBlockerGraphic = 0x1183;
    private static readonly ClientVersion StygianAbyss = new(7, 0, 0, 0);
    private static readonly ClientVersion HighSeas = new(7, 0, 9, 0);

    private readonly Dictionary<Serial, Viewer> _sessions = [];
    private readonly ISectorService _sectors;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;
    private readonly WorldConfig _world;
    private readonly IItemTemplateService? _templates;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    // Read on every use: the configured range of the live world (ultima.world.view_range).
    private int ViewRange => _world.ViewRange;

    public WorldViewService(
        ISectorService sectors,
        IMobileService mobiles,
        IItemService items,
        IPacketSendService sender,
        ITooltipService tooltips,
        WorldConfig world,
        ILogger? logger = null,
        IItemTemplateService? templates = null,
        TimeProvider? time = null
    )
    {
        _time = time ?? TimeProvider.System;
        _logger = logger ?? Log.ForContext<WorldViewService>();
        _templates = templates;
        _tooltips = tooltips;
        _sectors = sectors;
        _mobiles = mobiles;
        _items = items;
        _sender = sender;
        _world = world;
    }

    public void Entered(MobileEntity mobile, long sessionId, ClientVersion? version, AccountType account = AccountType.Regular)
    {
        var own = new Viewer(sessionId, version, account);
        _sessions[mobile.Id] = own;
        ShowAround(mobile, own);
    }

    public bool HasEntered(Serial mobile)
    {
        return _sessions.ContainsKey(mobile);
    }

    // Shows the mobile to the players in range and, when it is a player, everyone and every ground item in range to it.
    private void ShowAround(MobileEntity mobile, Viewer? own)
    {
        MobileIncomingPacket? incoming = null;
        var sent = new SentCounts();

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            if (own is not null && CanSee(own, other))
            {
                SendMobile(own.SessionId, other, Incoming(other));
                sent.Add(other);
            }

            if (_sessions.TryGetValue(other.Id, out var viewer) && CanSee(viewer, mobile))
            {
                SendMobile(viewer.SessionId, mobile, incoming ??= Incoming(mobile));
            }
        }

        if (own is null)
        {
            return;
        }

        foreach (var item in _sectors.GetItemsInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (SendItem(own, item))
            {
                sent.Items++;
            }
        }

        LogSectorEntry(mobile, sent);
    }

    public void Moved(MobileEntity mobile, Point3D oldLocation, bool running)
    {
        Relocated(mobile, oldLocation, running, false);
    }

    public void Teleported(MobileEntity mobile, MapType oldMap, Point3D oldLocation)
    {
        if (oldMap == mobile.Map)
        {
            Relocated(mobile, oldLocation, false, true);

            return;
        }

        var remove = new RemoveEntityPacket(mobile.Id);
        var own = _sessions.GetValueOrDefault(mobile.Id);

        // As ModernUO's ClearScreen: the mover's client is told to drop what it saw on the old map, whose coordinates
        // may be in range on the new one too.
        foreach (var other in _sectors.GetMobilesInRange(oldMap, oldLocation, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            if (_sessions.TryGetValue(other.Id, out var viewer) && CanSee(viewer, mobile))
            {
                _sender.TrySend(viewer.SessionId, remove);
            }

            if (own is not null)
            {
                _sender.TrySend(own.SessionId, new RemoveEntityPacket(other.Id));
            }
        }

        if (own is not null)
        {
            foreach (var item in _sectors.GetItemsInRange(oldMap, oldLocation, ViewRange))
            {
                _sender.TrySend(own.SessionId, new RemoveEntityPacket(item.Id));
            }
        }

        // The mover is shown the new surroundings from nothing.
        ShowAround(mobile, own);
    }

    private void Relocated(MobileEntity mobile, Point3D oldLocation, bool running, bool teleported)
    {
        // Players that saw the old tile but not the new one lose the mover.
        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, oldLocation, ViewRange))
        {
            if (other.Id != mobile.Id &&
                !InRange(other.Location, mobile.Location) &&
                _sessions.TryGetValue(other.Id, out var viewer) &&
                CanSee(viewer, mobile))
            {
                _sender.TrySend(viewer.SessionId, new RemoveEntityPacket(mobile.Id));
            }
        }

        var hasSession = _sessions.TryGetValue(mobile.Id, out var own);
        var sent = new SentCounts();
        MobileMovingPacket? moving = null;
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            var sawIt = InRange(other.Location, oldLocation);

            if (_sessions.TryGetValue(other.Id, out var viewer) && CanSee(viewer, mobile))
            {
                if (sawIt && !teleported)
                {
                    _sender.TrySend(viewer.SessionId, moving ??= Moving(mobile, running));
                }
                else
                {
                    SendMobile(viewer.SessionId, mobile, incoming ??= Incoming(mobile));
                }
            }

            // The mover's client drops what it walks away from by itself, as in ModernUO; it only needs the newcomers.
            if (!sawIt && hasSession && CanSee(own!, other))
            {
                SendMobile(own!.SessionId, other, Incoming(other));
                sent.Add(other);
            }
        }

        if (!hasSession)
        {
            return;
        }

        foreach (var item in _sectors.GetItemsInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (item.GroundLocation is { } spot && !InRange(spot, oldLocation) && SendItem(own!, item))
            {
                sent.Items++;
            }
        }

        if (SectorOf(mobile.Location) != SectorOf(oldLocation))
        {
            LogSectorEntry(mobile, sent);
        }
    }

    public void Left(MobileEntity mobile)
    {
        // Told even when it never registered: others may have seen it between the two login passes on the loop.
        _sessions.Remove(mobile.Id);
        var remove = new RemoveEntityPacket(mobile.Id);

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id != mobile.Id && _sessions.TryGetValue(other.Id, out var viewer) && CanSee(viewer, mobile))
            {
                _sender.TrySend(viewer.SessionId, remove);
            }
        }
    }

    public void MobileAppeared(MobileEntity mobile)
    {
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (other.Id != mobile.Id && _sessions.TryGetValue(other.Id, out var viewer) && CanSee(viewer, mobile))
            {
                SendMobile(viewer.SessionId, mobile, incoming ??= Incoming(mobile));
            }
        }
    }

    public void MobileAnimated(MobileEntity mobile, int action, int frameCount, int repeatCount)
    {
        var animation = new MobileAnimationPacket(mobile.Id, action, frameCount, repeatCount);

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (_sessions.TryGetValue(other.Id, out var viewer) && (other.Id == mobile.Id || CanSee(viewer, mobile)))
            {
                _sender.TrySend(viewer.SessionId, animation);
            }
        }
    }

    public void MobileDied(MobileEntity mobile, Serial corpse)
    {
        var death = new DeathAnimationPacket(mobile.Id, corpse);

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            // Not its own player: the client of who dies is told another way.
            if (other.Id != mobile.Id && _sessions.TryGetValue(other.Id, out var viewer) && CanSee(viewer, mobile))
            {
                _sender.TrySend(viewer.SessionId, death);
            }
        }
    }

    public void MobileFlagsChanged(MobileEntity mobile)
    {
        var moving = Moving(mobile, false);

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            // Its own player too: the client draws its own figure from these flags.
            if (_sessions.TryGetValue(other.Id, out var viewer) && (other.Id == mobile.Id || CanSee(viewer, mobile)))
            {
                _sender.TrySend(viewer.SessionId, moving);
            }
        }
    }

    public void MobileHiddenChanged(MobileEntity mobile)
    {
        MobileMovingPacket? moving = null;
        MobileIncomingPacket? incoming = null;

        foreach (var other in _sectors.GetMobilesInRange(mobile.Map, mobile.Location, ViewRange))
        {
            if (!_sessions.TryGetValue(other.Id, out var viewer))
            {
                continue;
            }

            if (other.Id == mobile.Id || viewer.Account >= AccountType.GameMaster)
            {
                // Itself and the staff, who see it either way: only its flags change.
                _sender.TrySend(viewer.SessionId, moving ??= Moving(mobile, false));
            }
            else if (mobile.Hidden)
            {
                _sender.TrySend(viewer.SessionId, new RemoveEntityPacket(mobile.Id));
            }
            else
            {
                SendMobile(viewer.SessionId, mobile, incoming ??= Incoming(mobile));
            }
        }
    }

    public void ItemAppeared(ItemEntity item)
    {
        if (item.Map is not { } map || item.GroundLocation is not { } spot)
        {
            return;
        }

        foreach (var other in _sectors.GetMobilesInRange(map, spot, ViewRange))
        {
            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                SendItem(viewer, item);
            }
        }
    }

    public void ShowItemTo(MobileEntity viewer, ItemEntity item)
    {
        if (item.GroundLocation is not null && _sessions.TryGetValue(viewer.Id, out var session))
        {
            SendItem(session, item);
        }
    }

    public void ItemDisappeared(ItemEntity item)
    {
        if (item.Map is not { } map || item.GroundLocation is not { } spot)
        {
            return;
        }

        // A corpse that is gone gives back the serials of its hair; shown again, it takes new ones.
        if (item.ItemId == CorpseProps.Graphic)
        {
            _mobiles.ForgetHair(item.Id);
        }

        var remove = new RemoveEntityPacket(item.Id);

        foreach (var other in _sectors.GetMobilesInRange(map, spot, ViewRange))
        {
            if (_sessions.TryGetValue(other.Id, out var viewer))
            {
                _sender.TrySend(viewer.SessionId, remove);
            }
        }
    }

    public void ContainedItemAppeared(ItemEntity item, ItemEntity root, Serial except)
    {
        var info = _tooltips.Info(item);

        foreach (var viewer in AroundTheContainer(root, except))
        {
            _sender.TrySend(
                viewer.SessionId,
                new ContainerItemUpdatePacket(item, GameSessionClientExtensions.UsesContainerGrid(viewer.Version))
            );
            _sender.TrySend(viewer.SessionId, info);
        }
    }

    public void ContainedItemDisappeared(ItemEntity item, ItemEntity root, Serial except)
    {
        var remove = new RemoveEntityPacket(item.Id);

        foreach (var viewer in AroundTheContainer(root, except))
        {
            _sender.TrySend(viewer.SessionId, remove);
        }
    }

    // The players who may have the container on the ground open: those in range of it, less the one who acts.
    private IEnumerable<Viewer> AroundTheContainer(ItemEntity root, Serial except)
    {
        if (root.Map is not { } map || root.GroundLocation is not { } spot)
        {
            yield break;
        }

        foreach (var other in _sectors.GetMobilesInRange(map, spot, ViewRange))
        {
            if (other.Id != except && _sessions.TryGetValue(other.Id, out var viewer))
            {
                yield return viewer;
            }
        }
    }

    public void WornItemChanged(MobileEntity wearer, ItemEntity item)
    {
        var worn = new WornItemPacket(item);
        var info = _tooltips.Info(item);

        foreach (var other in _sectors.GetMobilesInRange(wearer.Map, wearer.Location, ViewRange))
        {
            if (_sessions.TryGetValue(other.Id, out var viewer) && (other.Id == wearer.Id || CanSee(viewer, wearer)))
            {
                _sender.TrySend(viewer.SessionId, worn);
                _sender.TrySend(viewer.SessionId, info);
            }
        }
    }

    public void WornItemRemoved(MobileEntity wearer, ItemEntity item)
    {
        var remove = new RemoveEntityPacket(item.Id);

        foreach (var other in _sectors.GetMobilesInRange(wearer.Map, wearer.Location, ViewRange))
        {
            if (other.Id != wearer.Id && _sessions.TryGetValue(other.Id, out var viewer) && CanSee(viewer, wearer))
            {
                _sender.TrySend(viewer.SessionId, remove);
            }
        }
    }

    private static IOutgoingPacket WorldItem(ItemEntity item, ClientVersion? version, AccountType account)
    {
        var spot = item.GroundLocation!.Value;
        // As ModernUO's Blocker: the graphic that draws nothing blocks the way unseen; the staff sees a gravestone.
        var graphic = item.ItemId == NoDrawGraphic && account >= AccountType.GameMaster ? StaffBlockerGraphic : item.ItemId;

        // As ModernUO: 0xF3 from 7.0.0.0 (Stygian Abyss), two bytes longer from 7.0.9.0 (High Seas); unknown is newest.
        if (version is null || version.CompareTo(StygianAbyss) >= 0)
        {
            var highSeas = version is null || version.CompareTo(HighSeas) >= 0;

            return new WorldItemSaPacket(item.Id, graphic, AmountOf(item), spot, item.Hue, highSeas, LightOf(item));
        }

        return new WorldItemPacket(item.Id, graphic, AmountOf(item), spot, item.Hue, LightOf(item));
    }

    // A corpse tells the client its body in the place of the amount.
    private static int AmountOf(ItemEntity item)
    {
        return item.ItemId == CorpseProps.Graphic && WholeProp(item, CorpseProps.Body) is { } body ? body : item.Amount;
    }

    // A prop that holds a whole number from 0 to 65535, however it was written; null for anything else, as a script
    // may have put there.
    private static int? WholeProp(ItemEntity item, string key)
    {
        var number = item.Props?.GetValueOrDefault(key) switch
        {
            int value    => value,
            long value   => value,
            double value => value,
            _            => double.NaN
        };

        return number is >= 0 and <= ushort.MaxValue && Math.Floor(number) == number ? (int)number : null;
    }

    // The item's light shape, kept in its "light" prop by name, such as circle150; none for anything else. A corpse
    // tells the client the way it lies in the same byte, as ServUO does.
    private static int LightOf(ItemEntity item)
    {
        if (item.ItemId == CorpseProps.Graphic && WholeProp(item, CorpseProps.Direction) is { } direction)
        {
            return direction & 0xFF;
        }

        return item.Props?.GetValueOrDefault("light") is string name && EnumNameUtils.TryParse<LightType>(name, out var light)
            ? (int)light
            : 0;
    }

    private static (int X, int Y) SectorOf(Point3D location)
    {
        return (location.X / SectorService.SectorSize, location.Y / SectorService.SectorSize);
    }

    private void LogSectorEntry(MobileEntity mobile, SentCounts sent)
    {
        var (x, y) = SectorOf(mobile.Location);
        _logger.Debug(
            "{Name} entered sector ({SectorX}, {SectorY}) of {Map}: sent {Items} items, {Npcs} NPCs and {Players} players",
            mobile.Name,
            x,
            y,
            mobile.Map,
            sent.Items,
            sent.Npcs,
            sent.Players
        );
    }

    // A hidden mobile is on no player's screen; the staff sees it, as in ModernUO.
    private static bool CanSee(Viewer viewer, MobileEntity mobile)
    {
        return !mobile.Hidden || viewer.Account >= AccountType.GameMaster;
    }

    // The mobile, then the revision of its tooltip and of each worn item's, as ModernUO: the client asks for the
    // tooltips it does not have yet.

    private void SendMobile(long sessionId, MobileEntity mobile, MobileIncomingPacket incoming)
    {
        _sender.TrySend(sessionId, incoming);
        _sender.TrySend(sessionId, _tooltips.Info(mobile));

        // Not the bank box: the others never see it.
        foreach (var item in _items.GetWorn(mobile.Id).Where(item => item.Layer != LayerType.Bank))
        {
            _sender.TrySend(sessionId, _tooltips.Info(item));
        }
    }

    // False, with nothing sent, for an item hidden from the viewer's account, such as a teleporter from a player.
    private bool SendItem(Viewer viewer, ItemEntity item)
    {
        if (viewer.Account < VisibilityOf(item))
        {
            return false;
        }

        _sender.TrySend(viewer.SessionId, WorldItem(item, viewer.Version, viewer.Account));
        _sender.TrySend(viewer.SessionId, _tooltips.Info(item));
        SendCorpseDress(viewer, item);

        return true;
    }

    // As ModernUO: after the corpse of a human body, what it is drawn wearing. First the items (0x3C), then their
    // layers (0x89): what who died wore that is still inside, and its hair and beard, which are no items and get the
    // virtual serials a mobile's do.
    private void SendCorpseDress(Viewer viewer, ItemEntity corpse)
    {
        if (corpse.ItemId != CorpseProps.Graphic ||
            WholeProp(corpse, CorpseProps.Body) is not { } body ||
            !CorpseProps.IsHumanBody(body))
        {
            return;
        }

        // Who died is still falling on the screens: its clothes stay on it until the fall is over.
        if (corpse.Props?.GetValueOrDefault(CorpseProps.DressAt) is long or int or double &&
            corpse.GetProp<long>(CorpseProps.DressAt) > _time.GetUtcNow().ToUnixTimeMilliseconds())
        {
            return;
        }

        var inside = _items.GetContents(corpse.Id).ToDictionary(item => item.Id);
        var entries = new List<ContainerItemEntry>();
        var worn = new List<CorpseWornItem>();
        var layers = new HashSet<LayerType>();

        foreach (var (serial, layer) in WornOf(corpse))
        {
            // One item a layer; taken out of the corpse, or hidden from this viewer, it is not drawn.
            if (inside.TryGetValue(serial, out var item) && viewer.Account >= VisibilityOf(item) && layers.Add(layer))
            {
                entries.Add(new(item.Id, item.ItemId, item.Amount, item.GridX ?? 0, item.GridY ?? 0, 0, corpse.Id, item.Hue));
                worn.Add(new(layer, item.Id));
            }
        }

        // A virtual serial is taken only by a corpse that has the hair to show; ItemDisappeared gives it back.
        if (HairOf(corpse, CorpseProps.Hair, CorpseProps.HairHue) is { } hair)
        {
            var serial = _mobiles.HairSerial(corpse.Id);
            entries.Add(new(serial, hair.Graphic, 1, 0, 0, 0, corpse.Id, hair.Hue));
            worn.Add(new(LayerType.Hair, serial));
        }

        if (HairOf(corpse, CorpseProps.Beard, CorpseProps.BeardHue) is { } beard)
        {
            var serial = _mobiles.BeardSerial(corpse.Id);
            entries.Add(new(serial, beard.Graphic, 1, 0, 0, 0, corpse.Id, beard.Hue));
            worn.Add(new(LayerType.FacialHair, serial));
        }

        if (worn.Count == 0)
        {
            return;
        }

        var grid = GameSessionClientExtensions.UsesContainerGrid(viewer.Version);
        _sender.TrySend(viewer.SessionId, ContainerContentPacket.Of(entries, grid));
        _sender.TrySend(viewer.SessionId, new CorpseEquipmentPacket(corpse.Id, worn));
    }

    private static (int Graphic, Hue Hue)? HairOf(ItemEntity corpse, string graphicProp, string hueProp)
    {
        return WholeProp(corpse, graphicProp) is { } graphic and > 0
                   ? (graphic, new Hue((ushort)(WholeProp(corpse, hueProp) ?? 0)))
                   : null;
    }

    // The "serial:layer" pairs of the corpse's worn prop; what a script spoiled is left out.
    private static IEnumerable<(Serial Serial, LayerType Layer)> WornOf(ItemEntity corpse)
    {
        if (corpse.Props?.GetValueOrDefault(CorpseProps.Worn) is not string text)
        {
            yield break;
        }

        foreach (var pair in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(':');

            if (parts.Length == 2 &&
                uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var serial) &&
                byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var layer) &&
                // A layer something can be worn on: none is not one.
                layer != (byte)LayerType.None &&
                Enum.IsDefined((LayerType)layer))
            {
                yield return (new Serial(serial), (LayerType)layer);
            }
        }
    }

    // The item's own visibility, else its template's; everyone sees an item with neither.
    private AccountType VisibilityOf(ItemEntity item)
    {
        if (item.Visibility is { } own)
        {
            return own;
        }

        return _templates is not null && _templates.TryGet(item.TemplateId, out var template)
            ? template.Visibility ?? AccountType.Regular
            : AccountType.Regular;
    }

    private MobileIncomingPacket Incoming(MobileEntity mobile)
    {
        var worn = _items.GetWorn(mobile.Id);

        return new(
            mobile.Id,
            new Body((ushort)mobile.Body),
            mobile.Location,
            mobile.Direction,
            mobile.SkinHue,
            _mobiles.GetFlags(mobile),
            mobile.ShownNotoriety,
            _mobiles.GetEquipment(mobile, worn)
        );
    }

    private MobileMovingPacket Moving(MobileEntity mobile, bool running)
    {
        return new(
            mobile.Id,
            new Body((ushort)mobile.Body),
            mobile.Location,
            mobile.Direction,
            running,
            mobile.SkinHue,
            _mobiles.GetFlags(mobile),
            mobile.ShownNotoriety
        );
    }

    private bool InRange(Point3D a, Point3D b)
    {
        return Math.Abs(a.X - b.X) <= ViewRange && Math.Abs(a.Y - b.Y) <= ViewRange;
    }
}
