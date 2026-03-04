using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Entities;

/// <summary>
/// Lua-facing proxy exposing item fields used by scripts.
/// </summary>
public sealed class LuaItemProxy
{
    private readonly UOItemEntity _item;
    private readonly IItemService? _itemService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly ISpeechService? _speechService;

    public LuaItemProxy(
        UOItemEntity item,
        IItemService? itemService = null,
        ISpatialWorldService? spatialWorldService = null,
        ISpeechService? speechService = null
    )
    {
        _item = item;
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _speechService = speechService;
    }

    public uint Serial => (uint)_item.Id;

    public string Name => _item.Name ?? string.Empty;

    public int MapId => _item.MapId;

    public int LocationX => _item.Location.X;

    public int LocationY => _item.Location.Y;

    public int LocationZ => _item.Location.Z;

    public int Amount => _item.Amount;

    public int ItemId => _item.ItemId;

    public int Hue => _item.Hue;

    public string ScriptId => _item.ScriptId;

    public uint ParentContainerId => (uint)_item.ParentContainerId;

    public int ContainerX => _item.ContainerPosition.X;

    public int ContainerY => _item.ContainerPosition.Y;

    public DirectionType Direction => _item.Direction;

    public bool SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        _item.Name = name.Trim();

        return PersistItem();
    }

    public bool SetAmount(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        _item.Amount = amount;

        return PersistItem();
    }

    public bool AddAmount(int delta)
    {
        var nextAmount = _item.Amount + delta;

        if (nextAmount < 0)
        {
            return false;
        }

        _item.Amount = nextAmount;

        return PersistItem();
    }

    public bool SetHue(int hue)
    {
        if (hue < 0)
        {
            return false;
        }

        _item.Hue = hue;

        return PersistItem();
    }

    public bool SetScriptId(string scriptId)
    {
        if (string.IsNullOrWhiteSpace(scriptId))
        {
            return false;
        }

        _item.ScriptId = scriptId.Trim();

        return PersistItem();
    }

    public bool Delete()
    {
        if (_itemService is null)
        {
            return false;
        }

        return _itemService.DeleteItemAsync(_item.Id).GetAwaiter().GetResult();
    }

    public bool IsContainer()
        => _item.IsContainer;

    public bool IsInWorld()
        => _item.ParentContainerId == Moongate.UO.Data.Ids.Serial.Zero &&
           _item.EquippedMobileId == Moongate.UO.Data.Ids.Serial.Zero;

    public bool IsInContainer()
        => _item.ParentContainerId != Moongate.UO.Data.Ids.Serial.Zero;

    public bool IsEquipped()
        => _item.EquippedMobileId != Moongate.UO.Data.Ids.Serial.Zero;

    public bool MoveToWorld(int mapId, int x, int y, int z)
    {
        if (_itemService is null || mapId < 0)
        {
            return false;
        }

        var moved = _itemService.MoveItemToWorldAsync(_item.Id, new(x, y, z), mapId).GetAwaiter().GetResult();

        if (!moved)
        {
            return false;
        }

        _item.MapId = mapId;
        _item.Location = new(x, y, z);
        _item.ParentContainerId = Moongate.UO.Data.Ids.Serial.Zero;
        _item.EquippedMobileId = Moongate.UO.Data.Ids.Serial.Zero;
        _item.EquippedLayer = null;

        return true;
    }

    public bool MoveToContainer(uint containerSerial, int x, int y)
    {
        if (_itemService is null || containerSerial == 0)
        {
            return false;
        }

        var moved = _itemService.MoveItemToContainerAsync(_item.Id, (Serial)containerSerial, new(x, y))
                                .GetAwaiter()
                                .GetResult();

        if (!moved)
        {
            return false;
        }

        _item.ParentContainerId = (Serial)containerSerial;
        _item.ContainerPosition = new(x, y);
        _item.EquippedMobileId = Moongate.UO.Data.Ids.Serial.Zero;
        _item.EquippedLayer = null;

        return true;
    }

    public bool EquipTo(uint mobileSerial, int layer)
    {
        if (_itemService is null || mobileSerial == 0)
        {
            return false;
        }

        if (layer < byte.MinValue || layer > byte.MaxValue || !Enum.IsDefined(typeof(ItemLayerType), (byte)layer))
        {
            return false;
        }

        var equipLayer = (ItemLayerType)layer;
        var equipped = _itemService.EquipItemAsync(_item.Id, (Serial)mobileSerial, equipLayer).GetAwaiter().GetResult();

        if (!equipped)
        {
            return false;
        }

        _item.EquippedMobileId = (Serial)mobileSerial;
        _item.EquippedLayer = equipLayer;
        _item.ParentContainerId = Moongate.UO.Data.Ids.Serial.Zero;

        return true;
    }

    public int Say(string text, int range = 12)
    {
        if (_speechService is null || _spatialWorldService is null || string.IsNullOrWhiteSpace(text) || range <= 0)
        {
            return 0;
        }

        var recipients = _spatialWorldService.GetPlayersInRange(_item.Location, range, _item.MapId);
        var delivered = 0;

        foreach (var session in recipients)
        {
            if (_speechService.SendMessageFromServerAsync(session, text).GetAwaiter().GetResult())
            {
                delivered++;
            }
        }

        return delivered;
    }

    public bool PlaySound(int soundId)
    {
        if (_spatialWorldService is null || soundId < 0)
        {
            return false;
        }

        var packet = new PlaySoundEffectPacket(
            mode: 0x01,
            soundModel: (ushort)Math.Min(soundId, ushort.MaxValue),
            unknown3: 0,
            location: _item.Location
        );
        var recipients = _spatialWorldService.BroadcastToPlayersAsync(packet, _item.MapId, _item.Location)
                                            .GetAwaiter()
                                            .GetResult();

        return recipients > 0;
    }

    public bool SetProp(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key) || value is null)
        {
            return false;
        }

        switch (value)
        {
            case bool boolValue:
                _item.SetCustomBoolean(key, boolValue);
                break;
            case sbyte sbyteValue:
                _item.SetCustomInteger(key, sbyteValue);
                break;
            case byte byteValue:
                _item.SetCustomInteger(key, byteValue);
                break;
            case short shortValue:
                _item.SetCustomInteger(key, shortValue);
                break;
            case ushort ushortValue:
                _item.SetCustomInteger(key, ushortValue);
                break;
            case int intValue:
                _item.SetCustomInteger(key, intValue);
                break;
            case uint uintValue:
                _item.SetCustomInteger(key, uintValue);
                break;
            case long longValue:
                _item.SetCustomInteger(key, longValue);
                break;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                _item.SetCustomInteger(key, (long)ulongValue);
                break;
            case float floatValue:
                _item.SetCustomDouble(key, floatValue);
                break;
            case double doubleValue:
                _item.SetCustomDouble(key, doubleValue);
                break;
            case decimal decimalValue:
                _item.SetCustomDouble(key, (double)decimalValue);
                break;
            case string stringValue:
                _item.SetCustomString(key, stringValue);
                break;
            default:
                return false;
        }

        return PersistItem();
    }

    public object? GetProp(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (!_item.CustomProperties.TryGetValue(key, out var property))
        {
            return null;
        }

        return property.Type switch
        {
            ItemCustomPropertyType.Integer => property.IntegerValue,
            ItemCustomPropertyType.Boolean => property.BooleanValue,
            ItemCustomPropertyType.Double  => property.DoubleValue,
            ItemCustomPropertyType.String  => property.StringValue,
            _                              => null
        };
    }

    public bool RemoveProp(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!_item.RemoveCustomProperty(key))
        {
            return false;
        }

        return PersistItem();
    }

    private bool PersistItem()
    {
        if (_itemService is null)
        {
            return false;
        }

        _itemService.UpsertItemAsync(_item).GetAwaiter().GetResult();

        return true;
    }
}
