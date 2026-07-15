using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.World;
using Moongate.Server.Types;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default <see cref="IEnterWorldService" />: builds the self-only enter-world burst from a mobile's
/// state and streams it in the ModernUO order. Broadcasting nearby mobiles/items (SendEverything)
/// waits on a spatial world system and is out of scope here.
/// </summary>
public sealed class EnterWorldService : IEnterWorldService
{
    private const byte DefaultSeason = 0;     // spring
    private const byte OverallLightLevel = 0; // full daylight
    private const byte PersonalLightLevel = 0;
    private const byte FemaleFlag = 0x02;
    private const ushort DefaultStatCap = 225;
    private const byte DefaultFollowersMax = 5;

    // Self-only view: two fixed top-of-range virtual serials for the hair/beard pseudo-items. A
    // per-mobile virtual-serial allocator lands with the nearby-mobile broadcast.
    private static readonly Serial HairVirtualSerial = new(0x7FFFFFFF);
    private static readonly Serial FacialHairVirtualSerial = new(0x7FFFFFFE);

    private readonly IItemService _items;

    public EnterWorldService(IItemService items)
    {
        _items = items;
    }

    public void SendEnterWorld(PlayerSession session, MobileEntity mobile)
    {
        foreach (var packet in BuildSequence(mobile))
        {
            session.Send(packet);
        }

        session.SetState(SessionStateType.InWorld);
    }

    /// <summary>
    /// Builds the ordered self-only enter-world packet sequence for <paramref name="mobile" /> without
    /// sending it, so the ordering and contents can be inspected in isolation.
    /// </summary>
    public IReadOnlyList<IOutgoingPacket> BuildSequence(MobileEntity mobile)
    {
        var (mapWidth, mapHeight) = MapDimensions.Get(mobile.MapId);
        var position = mobile.Position;
        var body = (ushort)mobile.Body;
        var flags = GetBodyFlags(mobile);

        return
        [
            new LoginConfirmPacket(
                mobile.Id,
                body,
                (ushort)position.X,
                (ushort)position.Y,
                (short)position.Z,
                mobile.Direction,
                (ushort)mapWidth,
                (ushort)mapHeight
            ),
            new MapChangePacket((MapType)mobile.MapId),
            new MapPatchesPacket(),
            new SeasonChangePacket(DefaultSeason, true),
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
                NotorietyType.Innocent,
                BuildEquipment(mobile)
            ),
            BuildStatus(mobile),
            new WarModePacket(false),
            new LoginCompletePacket(),
            new GameTimePacket(0, 0, 0)
        ];
    }

    private List<MobileIncomingItem> BuildEquipment(MobileEntity mobile)
    {
        var items = new List<MobileIncomingItem>();

        foreach (var item in _items.GetEquipped(mobile))
        {
            if (item.EquippedLayer is not { } layer)
            {
                continue;
            }

            items.Add(new MobileIncomingItem(item.Id, (ushort)item.ItemId, layer, item.Hue));
        }

        if (mobile.HairStyle != 0)
        {
            items.Add(new MobileIncomingItem(HairVirtualSerial, mobile.HairStyle, LayerType.Hair, mobile.HairHue));
        }

        if (mobile.FacialHairStyle != 0)
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
            DefaultStatCap,
            DefaultFollowersMax
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
