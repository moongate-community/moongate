using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Events;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Server.Interfaces.World;
using Moongate.Server.Types;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Mobiles;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default <see cref="IWorldService" />: builds the self-only enter-world burst from a mobile's state,
/// streams it in the ModernUO order, and raises <see cref="PlayerEnteredWorldEvent" />. Broadcasting
/// nearby mobiles/items (SendEverything) waits on a spatial world system and is out of scope here.
/// </summary>
public sealed class WorldService : IWorldService
{
    private const byte OverallLightLevel = 0; // full daylight
    private const byte PersonalLightLevel = 0;
    private const byte FemaleFlag = 0x02;

    // Skills carry no per-skill cap or lock of their own yet, so every entry reports the classic
    // 100.0 ceiling and a free-to-gain arrow.
    private const ushort DefaultSkillCap = 1000;

    // Self-only view: two fixed top-of-range virtual serials for the hair/beard pseudo-items. A
    // per-mobile virtual-serial allocator lands with the nearby-mobile broadcast.
    private static readonly Serial HairVirtualSerial = new(0x7FFFFFFF);
    private static readonly Serial FacialHairVirtualSerial = new(0x7FFFFFFE);

    private readonly IItemService _items;
    private readonly ISkillService _skills;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _timeProvider;

    public WorldService(IItemService items, ISkillService skills, IEventBus eventBus, TimeProvider timeProvider)
    {
        _items = items;
        _skills = skills;
        _eventBus = eventBus;
        _timeProvider = timeProvider;
    }

    public void SendEnterWorld(PlayerSession session, MobileEntity mobile)
    {
        foreach (var packet in BuildSequence(mobile))
        {
            session.Send(packet);
        }

        session.SetState(SessionStateType.InWorld);

        _eventBus.Publish(new PlayerEnteredWorldEvent(session.SessionId, session.AccountId, mobile));
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

        return
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

            skills.Add(new SkillEntry((ushort)definition.Id, value, value, skillLock, cap));
        }

        return skills;
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

            items.Add(new MobileIncomingItem(item.Id, (ushort)item.ItemId, layer, item.Hue));
        }

        if (mobile.HairStyle != 0 && takenLayers.Add(LayerType.Hair))
        {
            items.Add(new MobileIncomingItem(HairVirtualSerial, mobile.HairStyle, LayerType.Hair, mobile.HairHue));
        }

        if (mobile.FacialHairStyle != 0 && takenLayers.Add(LayerType.FacialHair))
        {
            items.Add(
                new MobileIncomingItem(
                    FacialHairVirtualSerial,
                    mobile.FacialHairStyle,
                    LayerType.FacialHair,
                    mobile.FacialHairHue
                )
            );
        }

        return items;
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
