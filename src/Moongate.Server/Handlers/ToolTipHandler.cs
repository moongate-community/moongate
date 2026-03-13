using System.Collections.Concurrent;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Helpers;
using Moongate.Network.Packets.Incoming.Tooltip;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.System;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.MegaCliloc;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.MegaClilocPacket)]

/// <summary>
/// Represents ToolTipHandler.
/// </summary>
public class ToolTipHandler : BasePacketListener
{
    private readonly ConcurrentDictionary<Serial, CachedItemTooltip> _itemTooltipCache = [];
    private readonly ILogger _logger = Log.ForContext<ToolTipHandler>();
    private readonly IPersistenceService _persistenceService;

    private readonly record struct CachedItemTooltip(int ItemHashCode, ObjectPropertyList PacketTemplate);

    public ToolTipHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IPersistenceService persistenceService
    )
        : base(outgoingPacketQueue)
    {
        _persistenceService = persistenceService;
    }

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
                RemoveAndDisposeCachedTooltip(serial);

                return null;
            }

            var itemHashCode = item.GetHashCode();

            if (_itemTooltipCache.TryGetValue(serial, out var cachedTooltip) &&
                cachedTooltip.ItemHashCode == itemHashCode)
            {
                return cachedTooltip.PacketTemplate.Clone();
            }

            var propertyList = CreateItemPropertyList(item);

            ReplaceCachedTooltip(serial, new(itemHashCode, propertyList));

            return propertyList.Clone();
        }

        _logger.Debug("MegaCliloc request ignored. Invalid serial {Serial}.", serial);

        return null;
    }

    internal static ObjectPropertyList CreateItemPropertyList(UOItemEntity item)
    {
        var displayName = item.Name;

        if (item.TryGetCustomString(ItemCustomParamKeys.Book.Title, out var bookTitle) &&
            !string.IsNullOrWhiteSpace(bookTitle))
        {
            displayName = bookTitle;
        }

        var propertyList = MegaClilocBuilder.CreateItemTooltip(
            item.Id,
            displayName,
            item.ItemId,
            item.Amount,
            item.Weight,
            hue: item.Hue
        );

        if (item.TryGetCustomString(ItemCustomParamKeys.Book.Author, out var bookAuthor) &&
            !string.IsNullOrWhiteSpace(bookAuthor))
        {
            propertyList.Add($"by {bookAuthor}");
        }

        if (item.Rarity != ItemRarity.None)
        {
            propertyList.Add(CommonClilocIds.ItemRarity, item.Rarity.ToString());
        }

        AppendTypedItemProperties(propertyList, item);

        if (item.TryGetCustomString("label_number", out var value) &&
            uint.TryParse(value, out var clilocId))
        {
            propertyList.Replace(CommonClilocIds.ObjectName, clilocId);
        }

        return propertyList;
    }

    private static void AppendTypedItemProperties(ObjectPropertyList propertyList, UOItemEntity item)
    {
        var combatStats = item.CombatStats;

        if (combatStats is not null)
        {
            if (combatStats.DamageMin > 0 || combatStats.DamageMax > 0)
            {
                propertyList.Add(CommonClilocIds.WeaponDamage, $"{combatStats.DamageMin}\t{combatStats.DamageMax}");
            }

            if (combatStats.AttackSpeed > 0)
            {
                propertyList.Add(CommonClilocIds.WeaponSpeed, combatStats.AttackSpeed);
            }

            if (combatStats.CurrentDurability > 0 || combatStats.MaxDurability > 0)
            {
                MegaClilocBuilder.AddDurability(
                    propertyList,
                    combatStats.CurrentDurability,
                    combatStats.MaxDurability
                );
            }
        }

        var modifiers = item.Modifiers;

        if (modifiers is null)
        {
            return;
        }

        AddIfPositive(propertyList, CommonClilocIds.PhysicalResist, modifiers.PhysicalResist);
        AddIfPositive(propertyList, CommonClilocIds.FireResist, modifiers.FireResist);
        AddIfPositive(propertyList, CommonClilocIds.ColdResist, modifiers.ColdResist);
        AddIfPositive(propertyList, CommonClilocIds.PoisonResist, modifiers.PoisonResist);
        AddIfPositive(propertyList, CommonClilocIds.EnergyResist, modifiers.EnergyResist);
        AddIfPositive(propertyList, CommonClilocIds.HitChanceIncrease, modifiers.HitChanceIncrease);
        AddIfPositive(propertyList, CommonClilocIds.DamageIncrease, modifiers.DamageIncrease);
        AddIfPositive(propertyList, CommonClilocIds.SwingSpeedIncrease, modifiers.SwingSpeedIncrease);
        AddIfPositive(propertyList, CommonClilocIds.SpellDamageIncrease, modifiers.SpellDamageIncrease);
        AddIfPositive(propertyList, CommonClilocIds.FasterCasting, modifiers.FasterCasting);
        AddIfPositive(propertyList, CommonClilocIds.FasterCastRecovery, modifiers.FasterCastRecovery);

        if (modifiers.SpellChanneling != 0)
        {
            MegaClilocBuilder.AddSpellChanneling(propertyList);
        }

        if (modifiers.UsesRemaining > 0)
        {
            MegaClilocBuilder.AddUsesRemaining(propertyList, modifiers.UsesRemaining);
        }
    }

    private static void AddIfPositive(ObjectPropertyList propertyList, uint clilocId, int value)
    {
        if (value > 0)
        {
            propertyList.Add(clilocId, value);
        }
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

    private void RemoveAndDisposeCachedTooltip(Serial serial)
    {
        if (_itemTooltipCache.TryRemove(serial, out var removed))
        {
            removed.PacketTemplate.Dispose();
        }
    }

    private void ReplaceCachedTooltip(Serial serial, CachedItemTooltip updated)
    {
        if (_itemTooltipCache.TryGetValue(serial, out var previous))
        {
            previous.PacketTemplate.Dispose();
        }

        _itemTooltipCache[serial] = updated;
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
