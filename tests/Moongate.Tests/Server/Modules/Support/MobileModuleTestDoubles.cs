using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Interaction;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules.Support;

internal sealed class MobileModuleTestSpeechService : ISpeechService
{
    public Task<int> BroadcastFromServerAsync(
        string text,
        short hue = 946,
        short font = 3,
        string language = "ENU"
    )
    {
        _ = text;
        _ = hue;
        _ = font;
        _ = language;

        return Task.FromResult(0);
    }

    public Task HandleOpenChatWindowAsync(
        GameSession session,
        OpenChatWindowPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        _ = session;
        _ = packet;
        _ = cancellationToken;

        return Task.CompletedTask;
    }

    public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
        GameSession session,
        UnicodeSpeechPacket speechPacket,
        CancellationToken cancellationToken = default
    )
    {
        _ = session;
        _ = speechPacket;
        _ = cancellationToken;

        return Task.FromResult<UnicodeSpeechMessagePacket?>(null);
    }

    public Task<bool> SendMessageFromServerAsync(
        GameSession session,
        string text,
        short hue = 946,
        short font = 3,
        string language = "ENU"
    )
    {
        _ = session;
        _ = text;
        _ = hue;
        _ = font;
        _ = language;

        return Task.FromResult(true);
    }

    public Task<int> SpeakAsMobileAsync(
        UOMobileEntity speaker,
        string text,
        int range = 12,
        ChatMessageType messageType = ChatMessageType.Regular,
        short hue = SpeechHues.Default,
        short font = SpeechHues.DefaultFont,
        string language = "ENU",
        CancellationToken cancellationToken = default
    )
    {
        _ = speaker;
        _ = text;
        _ = range;
        _ = messageType;
        _ = hue;
        _ = font;
        _ = language;
        _ = cancellationToken;

        return Task.FromResult(0);
    }
}

internal sealed class MobileModuleTestCharacterService : ICharacterService
{
    public UOMobileEntity? CharacterToReturn { get; set; }

    public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
    {
        _ = accountId;
        _ = characterId;

        return Task.FromResult(true);
    }

    public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
    {
        _ = characterId;
        _ = shirtHue;
        _ = pantsHue;

        return Task.CompletedTask;
    }

    public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
    {
        _ = character;

        return Task.FromResult((Serial)1u);
    }

    public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
    {
        _ = character;

        return Task.FromResult<UOItemEntity?>(null);
    }

    public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
    {
        _ = character;

        return Task.FromResult<UOItemEntity?>(null);
    }

    public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
    {
        _ = characterId;

        return Task.FromResult(CharacterToReturn);
    }

    public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
    {
        _ = accountId;

        return Task.FromResult(new List<UOMobileEntity>());
    }

    public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
    {
        _ = accountId;
        _ = characterId;

        return Task.FromResult(true);
    }
}

internal sealed class MobileModuleTestMobileService : IMobileService
{
    private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();
    public List<Serial> CreateOrUpdateCalls { get; } = [];
    public Serial LastRiderId { get; private set; } = Serial.Zero;
    public Serial LastMountId { get; private set; } = Serial.Zero;
    public int DismountCalls { get; private set; }
    public string? LastSpawnTemplateId { get; private set; }
    public Point3D LastSpawnLocation { get; private set; } = Point3D.Zero;
    public int LastSpawnMapId { get; private set; }

    public UOMobileEntity? SpawnedMobile
    {
        get => _spawnedMobile;
        set
        {
            _spawnedMobile = value;

            if (value is not null)
            {
                _mobiles[value.Id] = value;
            }
        }
    }

    private UOMobileEntity? _spawnedMobile;

    public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
    {
        CreateOrUpdateCalls.Add(mobile.Id);
        _mobiles[mobile.Id] = mobile;

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> DismountAsync(Serial riderId, CancellationToken cancellationToken = default)
    {
        LastRiderId = riderId;
        DismountCalls++;

        if (_mobiles.TryGetValue(riderId, out var rider))
        {
            var mountId = rider.MountedMobileId;
            rider.MountedMobileId = Serial.Zero;
            rider.MountedDisplayItemId = 0;

            if (mountId != Serial.Zero && _mobiles.TryGetValue(mountId, out var mount))
            {
                mount.RiderMobileId = Serial.Zero;
            }
        }

        return Task.FromResult(true);
    }

    public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        => Task.FromResult(_mobiles.GetValueOrDefault(id));

    public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
        int mapId,
        int sectorX,
        int sectorY,
        CancellationToken cancellationToken = default
    )
        => Task.FromResult(new List<UOMobileEntity>());

    public void Register(UOMobileEntity mobile)
        => _mobiles[mobile.Id] = mobile;

    public Task<UOMobileEntity> SpawnFromTemplateAsync(
        string templateId,
        Point3D location,
        int mapId,
        Serial? accountId = null,
        CancellationToken cancellationToken = default
    )
    {
        _ = accountId;
        _ = cancellationToken;

        LastSpawnTemplateId = templateId;
        LastSpawnLocation = location;
        LastSpawnMapId = mapId;

        return Task.FromResult(
            SpawnedMobile ??
            new UOMobileEntity
            {
                Id = (Serial)0x400,
                Name = "Horse",
                MapId = mapId,
                Location = location
            }
        );
    }

    public Task<bool> TryMountAsync(Serial riderId, Serial mountId, CancellationToken cancellationToken = default)
    {
        LastRiderId = riderId;
        LastMountId = mountId;

        if (_mobiles.TryGetValue(riderId, out var rider) && _mobiles.TryGetValue(mountId, out var mount))
        {
            rider.MountedMobileId = mountId;
            mount.RiderMobileId = riderId;
        }

        return Task.FromResult(true);
    }

    public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
        string templateId,
        Point3D location,
        int mapId,
        Serial? accountId = null,
        CancellationToken cancellationToken = default
    )
        => Task.FromResult((false, (UOMobileEntity?)null));
}

internal sealed class MobileModuleTestItemService : IItemService
{
    public UOItemEntity? SpawnedItem { get; set; }
    public UOItemEntity? LastUpsertedItem { get; private set; }
    public Serial LastDeletedItemId { get; private set; } = Serial.Zero;
    public Serial LastMoveItemId { get; private set; } = Serial.Zero;
    public Serial LastContainerId { get; private set; } = Serial.Zero;
    public Point2D LastContainerPosition { get; private set; } = Point2D.Zero;

    public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
        => Task.CompletedTask;

    public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
        => item;

    public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
        => Task.FromResult<UOItemEntity?>(null);

    public Task<Serial> CreateItemAsync(UOItemEntity item)
        => Task.FromResult(item.Id);

    public Task<bool> DeleteItemAsync(Serial itemId)
    {
        LastDeletedItemId = itemId;

        return Task.FromResult(true);
    }

    public Task<DropItemToGroundResult?> DropItemToGroundAsync(
        Serial itemId,
        Point3D location,
        int mapId,
        long sessionId = 0
    )
        => Task.FromResult<DropItemToGroundResult?>(null);

    public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
        => Task.FromResult(true);

    public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
        => Task.FromResult(new List<UOItemEntity>());

    public Task<UOItemEntity?> GetItemAsync(Serial itemId)
        => Task.FromResult(SpawnedItem?.Id == itemId ? SpawnedItem : null);

    public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
        => Task.FromResult(new List<UOItemEntity>());

    public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
    {
        LastMoveItemId = itemId;
        LastContainerId = containerId;
        LastContainerPosition = position;

        return Task.FromResult(true);
    }

    public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        => Task.FromResult(true);

    public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        => Task.FromResult(
            SpawnedItem ??=
                new UOItemEntity
                {
                    Id = (Serial)0x800,
                    Name = itemTemplateId,
                    ItemId = 0x0F3F,
                    Amount = 1,
                    IsStackable = true,
                    MapId = 0,
                    Location = Point3D.Zero
                }
        );

    public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
        => Task.FromResult((SpawnedItem?.Id == itemId, SpawnedItem));

    public Task UpsertItemAsync(UOItemEntity item)
    {
        LastUpsertedItem = item;

        return Task.CompletedTask;
    }

    public Task UpsertItemsAsync(params UOItemEntity[] items)
        => Task.CompletedTask;
}
