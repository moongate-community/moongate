using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Helpers;
using Moongate.Network.Packets.Incoming.Tooltip;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.MegaCliloc;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.MegaClilocPacket)]

/// <summary>
/// Represents ToolTipHandler.
/// </summary>
public class ToolTipHandler : BasePacketListener
{
    private readonly ILogger _logger = Log.ForContext<ToolTipHandler>();
    private readonly IPersistenceService _persistenceService;

    public ToolTipHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IPersistenceService persistenceService
    )
        : base(outgoingPacketQueue)
        => _persistenceService = persistenceService;

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is MegaClilocPacket clilocPacket)
        {
            return await HandleMegaClilocPacketAsync(session, clilocPacket);
        }

        return true;
    }

    private async Task<IGameNetworkPacket?> CreatePropertyListAsync(GameSession session, Serial serial)
    {
        if (serial.IsMobile)
        {
            var mobile = await ResolveMobileAsync(session, serial);

            if (mobile is null)
            {
                _logger.Debug("MegaCliloc request ignored. Unknown mobile serial {Serial}.", serial);

                return null;
            }

            var name = string.IsNullOrWhiteSpace(mobile.Name) ? $"Mobile 0x{mobile.Id.Value:X8}" : mobile.Name;
            var maxHits = mobile.MaxHits > 0 ? mobile.MaxHits : Math.Max(mobile.Hits, 1);
            var maxMana = mobile.MaxMana > 0 ? mobile.MaxMana : Math.Max(mobile.Mana, 1);
            var maxStamina = mobile.MaxStamina > 0 ? mobile.MaxStamina : Math.Max(mobile.Stamina, 1);

            return MegaClilocBuilder.CreateMobileTooltip(
                mobile.Id,
                name,
                mobile.Hits,
                maxHits,
                mobile.Mana,
                maxMana,
                mobile.Stamina,
                maxStamina,
                mobile.IsPlayer
            );
        }

        if (serial.IsItem)
        {
            var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(serial);

            if (item is null)
            {
                _logger.Debug("MegaCliloc request ignored. Unknown item serial {Serial}.", serial);

                return null;
            }

            var propertyList = MegaClilocBuilder.CreateItemTooltip(
                item.Id,
                item.Name,
                item.ItemId,
                item.Amount,
                hue: item.Hue
            );
            propertyList.Add(CommonClilocIds.ItemRarity, item.Rarity.ToString());

            return propertyList;
        }

        _logger.Debug("MegaCliloc request ignored. Invalid serial {Serial}.", serial);

        return null;
    }

    private async Task<bool> HandleMegaClilocPacketAsync(GameSession session, MegaClilocPacket clilocPacket)
    {
        if (!clilocPacket.IsClientRequest || clilocPacket.RequestedSerials.Count == 0)
        {
            return true;
        }

        foreach (var requestedSerial in clilocPacket.RequestedSerials)
        {
            var propertyList = await CreatePropertyListAsync(session, requestedSerial);

            if (propertyList is null)
            {
                continue;
            }

            Enqueue(session, propertyList);
        }

        return true;
    }

    private async Task<UOMobileEntity?> ResolveMobileAsync(GameSession session, Serial serial)
    {
        if (session.Character is not null && session.Character.Id == serial)
        {
            return session.Character;
        }

        return await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(serial);
    }
}
