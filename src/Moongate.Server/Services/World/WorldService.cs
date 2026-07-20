using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Mobiles;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default <see cref="IWorldService" />: builds the self-only enter-world burst from a mobile's state,
/// streams it in the ModernUO order, and raises <see cref="PlayerEnteredWorldEvent" />. Also broadcasts
/// packets to in-world players near a point. Full SendEverything (nearby mobile/item snapshots) waits
/// on later spatial work and is out of scope here.
/// </summary>
public sealed class WorldService : IWorldService
{
    private const byte OverallLightLevel = 0; // full daylight
    private const byte PersonalLightLevel = 0;
    private const byte FemaleFlag = 0x02;

    // Skills carry no per-skill cap or lock of their own yet, so every entry reports the classic
    // 100.0 ceiling and a free-to-gain arrow.
    private const ushort DefaultSkillCap = 1000;

    private readonly IItemService _items;
    private readonly ISkillService _skills;
    private readonly IVirtualSerialService _virtualSerials;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _timeProvider;
    private readonly IOplService _opl;
    private readonly ISessionManager _sessions;

    public WorldService(
        IItemService items,
        ISkillService skills,
        IVirtualSerialService virtualSerials,
        IEventBus eventBus,
        TimeProvider timeProvider,
        IOplService opl,
        ISessionManager sessions
    )
    {
        _items = items;
        _skills = skills;
        _virtualSerials = virtualSerials;
        _eventBus = eventBus;
        _timeProvider = timeProvider;
        _opl = opl;
        _sessions = sessions;
    }

    public int Broadcast<TPacket>(TPacket packet) where TPacket : IOutgoingPacket
    {
        var recipients = 0;

        foreach (var session in _sessions.All)
        {
            if (session.State != SessionStateType.InWorld)
            {
                continue;
            }

            session.Send(packet);
            recipients++;
        }

        return recipients;
    }

    /// <summary>
    /// Builds the ordered self-only enter-world packet sequence for <paramref name="mobile" /> without
    /// sending it, so the ordering and contents can be inspected in isolation.
    /// </summary>
    public IReadOnlyList<IOutgoingPacket> BuildSequence(MobileEntity mobile)
    {
        var map = MapDefinitions.Get(mobile.MapId);
        var position = mobile.Position;
        var body = (ushort)mobile.Body;
        var flags = GetBodyFlags(mobile);
        var now = _timeProvider.GetLocalNow();

        List<IOutgoingPacket> packets =
        [
            new LoginConfirmPacket(
                mobile.Id,
                body,
                (ushort)position.X,
                (ushort)position.Y,
                (short)position.Z,
                mobile.Direction,
                (ushort)map.Width,
                (ushort)map.Height
            ),
            new MapChangePacket(map.Map),
            new MapPatchesPacket(),
            new SeasonChangePacket(map.Season, true),
            new SupportFeaturesPacket(FeatureFlagType.Modern),
            new MobileUpdatePacket(
                mobile.Id,
                body,
                mobile.SkinHue,
                flags,
                (ushort)position.X,
                (ushort)position.Y,
                (sbyte)position.Z,
                mobile.Direction
            ),
            new OverallLightLevelPacket(OverallLightLevel),
            new PersonalLightLevelPacket(mobile.Id, PersonalLightLevel),
            new MobileIncomingPacket(
                mobile.Id,
                body,
                (ushort)position.X,
                (ushort)position.Y,
                (sbyte)position.Z,
                mobile.Direction,
                mobile.SkinHue,
                flags,
                Notoriety.Resolve(mobile.Kills, mobile.Criminal),
                BuildEquipment(mobile)
            ),
            BuildStatus(mobile),

            // ModernUO pairs the lock state with the status (OnStatsQuery), so the arrows are right
            // from the first frame rather than only after the player touches one.
            new StatLockInfoPacket(mobile.Id, mobile.StrengthLock, mobile.DexterityLock, mobile.IntelligenceLock),
            new SkillsPacket(BuildSkills(mobile)),
            new WarModePacket(mobile.Warmode),
            new LoginCompletePacket(),
            new GameTimePacket((byte)now.Hour, (byte)now.Minute, (byte)now.Second)
        ];

        // Prime the client's tooltip cache for everything this burst just showed: itself and its
        // equipment. Objects without a property list get no revision to chase.
        AppendOplInfo(packets, mobile.Id);

        foreach (var equipped in _items.GetEquipped(mobile))
        {
            AppendOplInfo(packets, equipped.Id);
        }

        return packets;
    }

    /// <summary>
    /// True when a session in <paramref name="state" /> playing <paramref name="character" /> should
    /// receive a broadcast centered on <paramref name="center" />. Static so the filter is testable
    /// without fabricating live sessions.
    /// </summary>
    public static bool IsRecipient(
        SessionStateType state,
        MobileEntity? character,
        int mapId,
        Point3D center,
        int range,
        Serial? exclude
    )
        => state == SessionStateType.InWorld &&
           character is not null &&
           character.MapId == mapId &&
           character.Id != exclude &&
           center.InRange(character.Position, range);

    public void SendEnterWorld(PlayerSession session, MobileEntity mobile)
    {
        foreach (var packet in BuildSequence(mobile))
        {
            session.Send(packet);
        }

        session.SetState(SessionStateType.InWorld);

        _eventBus.Publish(new PlayerEnteredWorldEvent(session.SessionId, session.AccountId, mobile));
    }

    public int SendToPlayersInRange<TPacket>(int mapId, Point3D center, int range, TPacket packet, Serial? exclude = null)
        where TPacket : IOutgoingPacket
    {
        var recipients = 0;

        foreach (var session in _sessions.All)
        {
            if (!IsRecipient(session.State, session.Character, mapId, center, range, exclude))
            {
                continue;
            }

            session.Send(packet);
            recipients++;
        }

        return recipients;
    }

    private void AppendOplInfo(List<IOutgoingPacket> packets, Serial serial)
    {
        var snapshot = _opl.GetOrBuild(serial);

        if (snapshot.HasEntries)
        {
            packets.Add(new OplInfoPacket(serial, snapshot.Hash));
        }
    }

    /// <summary>
    /// Builds the worn items the client draws on a mobile: its equipment, then hair and facial hair as
    /// pseudo-items. One item per layer wins, as in ModernUO — the client cannot render two things on the
    /// same slot, and hair only goes out if nothing real already claimed its layer (a helm, say).
    /// </summary>
    private List<MobileIncomingItem> BuildEquipment(MobileEntity mobile)
    {
        var items = new List<MobileIncomingItem>();
        var takenLayers = new HashSet<LayerType>();

        foreach (var item in _items.GetEquipped(mobile))
        {
            if (item.EquippedLayer is not { } layer || !takenLayers.Add(layer))
            {
                continue;
            }

            items.Add(new(item.Id, (ushort)item.ItemId, layer, item.Hue));
        }

        if (mobile.HairStyle != 0 && takenLayers.Add(LayerType.Hair))
        {
            items.Add(
                new(
                    _virtualSerials.GetOrCreate(mobile.Id, LayerType.Hair),
                    mobile.HairStyle,
                    LayerType.Hair,
                    mobile.HairHue
                )
            );
        }

        if (mobile.FacialHairStyle != 0 && takenLayers.Add(LayerType.FacialHair))
        {
            items.Add(
                new(
                    _virtualSerials.GetOrCreate(mobile.Id, LayerType.FacialHair),
                    mobile.FacialHairStyle,
                    LayerType.FacialHair,
                    mobile.FacialHairHue
                )
            );
        }

        return items;
    }

    /// <summary>
    /// Builds the full skill list: every registered skill, including the ones this mobile never
    /// trained, because the client renders exactly the rows it is sent.
    /// </summary>
    private List<SkillEntry> BuildSkills(MobileEntity mobile)
    {
        var skills = new List<SkillEntry>(_skills.All.Count);

        foreach (var definition in _skills.All)
        {
            var skill = mobile.Skills.GetValueOrDefault(definition.Id);
            var value = (ushort)(skill?.Value ?? 0);
            var cap = (ushort)(skill?.Cap ?? DefaultSkillCap);
            var skillLock = skill?.Lock ?? SkillLockType.Up;

            skills.Add(new((ushort)definition.Id, value, value, skillLock, cap));
        }

        return skills;
    }

    private static MobileStatusPacket BuildStatus(MobileEntity mobile)
        => new(
            mobile.Id,
            mobile.Name,
            (ushort)mobile.Hits,
            (ushort)mobile.HitsMax,
            mobile.Gender == GenderType.Female,
            (ushort)mobile.Strength,
            (ushort)mobile.Dexterity,
            (ushort)mobile.Intelligence,
            (ushort)mobile.Stamina,
            (ushort)mobile.StaminaMax,
            (ushort)mobile.Mana,
            (ushort)mobile.ManaMax,
            mobile.Race,
            (ushort)mobile.StatCap,
            (byte)mobile.Followers,
            (byte)mobile.FollowersMax
        );

    private static byte GetBodyFlags(MobileEntity mobile)
    {
        byte flags = 0;

        if (mobile.Gender == GenderType.Female)
        {
            flags |= FemaleFlag;
        }

        return flags;
    }
}
