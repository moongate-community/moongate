using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Validates and applies player resurrection state changes for accepted offers.
/// </summary>
public sealed class ResurrectionService : IResurrectionService
{
    private const int AnkhRange = 2;
    private const int DeathShroudItemId = 0x204E;
    private const int HealerRange = 4;
    private const int ResurrectionHitDivisor = 10;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IMobileService _mobileService;
    private readonly IItemService _itemService;
    private readonly IMovementTileQueryService _movementTileQueryService;
    private readonly IGameEventBusService _gameEventBusService;

    public ResurrectionService(
        IGameNetworkSessionService gameNetworkSessionService,
        IMobileService mobileService,
        IItemService itemService,
        IMovementTileQueryService movementTileQueryService,
        IGameEventBusService gameEventBusService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _mobileService = mobileService;
        _itemService = itemService;
        _movementTileQueryService = movementTileQueryService;
        _gameEventBusService = gameEventBusService;
    }

    public async Task<bool> TryResurrectAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        CancellationToken cancellationToken = default
    )
    {
        var player = ResolvePlayer(sessionId, characterId);

        if (player is null)
        {
            return false;
        }

        return await TryResurrectAsync(
            sessionId,
            characterId,
            sourceType,
            player.Id,
            player.MapId,
            player.Location,
            cancellationToken
        );
    }

    public async Task<bool> TryResurrectAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        Serial sourceSerial,
        int mapId,
        Point3D sourceLocation,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceSerial == Serial.Zero)
        {
            return false;
        }

        var player = ResolvePlayer(sessionId, characterId);

        if (player is null || !player.IsPlayer || player.IsAlive)
        {
            return false;
        }

        var resolvedSource = await ResolveSourceAsync(sourceType, sourceSerial);

        if (
            resolvedSource is null ||
            resolvedSource.Value.MapId != mapId ||
            player.MapId != resolvedSource.Value.MapId ||
            !IsWithinSourceRange(player.Location, resolvedSource.Value.Location, sourceType)
        )
        {
            return false;
        }

        if (!_movementTileQueryService.CanFit(player.MapId, player.Location.X, player.Location.Y, player.Location.Z))
        {
            return false;
        }

        await RemoveDeathShroudAsync(player);

        player.IsAlive = true;
        player.Warmode = false;
        player.Hits = Math.Max(1, player.MaxHits / 10);
        player.BaseBody = 0x00;

        await _mobileService.CreateOrUpdateAsync(player, cancellationToken);
        await _gameEventBusService.PublishAsync(new MobileAppearanceChangedEvent(player), cancellationToken);

        return true;
    }

    private UOMobileEntity? ResolvePlayer(long sessionId, Serial characterId)
    {
        if (!_gameNetworkSessionService.TryGet(sessionId, out var session))
        {
            return null;
        }

        if (session.CharacterId != characterId || session.Character?.Id != characterId)
        {
            return null;
        }

        return session.Character;
    }

    private async Task RemoveDeathShroudAsync(UOMobileEntity player)
    {
        var shroud = player.GetEquippedItemsRuntime()
                           .FirstOrDefault(item => item.EquippedLayer == ItemLayerType.OuterTorso && IsDeathShroud(item));

        if (shroud is null && player.TryGetEquippedReference(ItemLayerType.OuterTorso, out var itemReference))
        {
            shroud = await _itemService.GetItemAsync(itemReference.Id);
        }

        if (shroud is null || !IsDeathShroud(shroud))
        {
            return;
        }

        _ = player.UnequipItem(ItemLayerType.OuterTorso, shroud);
        _ = await _itemService.DeleteItemAsync(shroud.Id);
    }

    private async Task<(int MapId, Point3D Location)?> ResolveSourceAsync(
        ResurrectionOfferSourceType sourceType,
        Serial sourceSerial
    )
    {
        switch (sourceType)
        {
            case ResurrectionOfferSourceType.Healer:
                {
                    var healer = await _mobileService.GetAsync(sourceSerial);

                    if (
                        healer is null ||
                        !healer.TryGetCustomString(Moongate.Server.Data.Internal.Scripting.MobileCustomParamKeys.Interaction.ResurrectionSource, out var healerSource) ||
                        !string.Equals(healerSource, "healer", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return null;
                    }

                    return (healer.MapId, healer.Location);
                }

            case ResurrectionOfferSourceType.Ankh:
                {
                    var ankh = await _itemService.GetItemAsync(sourceSerial);

                    if (
                        ankh is null ||
                        !ankh.TryGetCustomString(Moongate.Server.Data.Internal.Scripting.ItemCustomParamKeys.Interaction.ResurrectionSource, out var ankhSource) ||
                        !string.Equals(ankhSource, "ankh", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return null;
                    }

                    return (ankh.MapId, ankh.Location);
                }

            default:
                return null;
        }
    }

    private static bool IsWithinSourceRange(
        Point3D playerLocation,
        Point3D sourceLocation,
        ResurrectionOfferSourceType sourceType
    )
    {
        var allowedRange = sourceType switch
        {
            ResurrectionOfferSourceType.Healer => HealerRange,
            ResurrectionOfferSourceType.Ankh => AnkhRange,
            _ => 0
        };

        return playerLocation.GetDistance(sourceLocation) <= allowedRange;
    }

    private static bool IsDeathShroud(UOItemEntity item)
        => item.ItemId == DeathShroudItemId;
}
