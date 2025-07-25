using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Context;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Packets.Chat;
using Moongate.UO.Data.Packets.Effects;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Packets.Objects;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Services;
using Moongate.UO.Interfaces.Services.Systems;
using Serilog;

namespace Moongate.Server.Services;

public class NotificationSystem : INotificationSystem
{
    private readonly ILogger _logger = Log.ForContext<NotificationSystem>();

    private readonly IGameSessionService _gameSessionService;
    private readonly IMobileService _mobileService;
    private readonly ICommandSystemService _commandSystemService;
    private readonly ISpatialWorldService _spatialWorldService;

    public NotificationSystem(
        IMobileService mobileService, IGameSessionService gameSessionService, ICommandSystemService commandSystemService,
        ISpatialWorldService spatialWorldService
    )
    {
        _mobileService = mobileService;
        _gameSessionService = gameSessionService;
        _commandSystemService = commandSystemService;
        _spatialWorldService = spatialWorldService;

        _gameSessionService.GameSessionCreated += OnGameSessionCreated;
        _gameSessionService.GameSessionBeforeDestroy += OnGameSessionBeforeDestroy;

        _spatialWorldService.MobileSectorMoved += OnMobileSectorMoved;
        _spatialWorldService.MobileMoved += OnMobileMoved;

        _spatialWorldService.ItemMovedOnGround += OnItemOnGround;
        _spatialWorldService.ItemRemoved += OnItemRemoved;

        _spatialWorldService.OnMobileAddedInSector += OnOnMobileAddedInSector;


        _mobileService.MobileAdded += OnMobileAdded;
    }

    private void OnOnMobileAddedInSector(UOMobileEntity mobile, MapSector sector, WorldView worldView)
    {
        _logger.Debug("Mobile {MobileId} {Name} added to sector {Sector}", mobile.Id, mobile.Name, sector);

        foreach (var mobileInView in worldView.NearbyMobiles)
        {
            if (mobileInView.IsPlayer)
            {
                var gameSession = _gameSessionService.GetGameSessionByMobile(mobileInView);

                if (gameSession != null)
                {
                    gameSession.SendPackets(new DrawGamePlayerPacket(mobile));

                    gameSession.SendPackets(new MobileDrawPacket(mobile, mobile, true, true));

                    // gameSession.SendPackets(new WornItemsPacket(mobile));
                }
            }
        }
    }

    private void OnItemRemoved(UOItemEntity item, Point3D oldLocation, Point3D newLocation, List<UOMobileEntity> mobiles)
    {
    }

    private void OnItemOnGround(UOItemEntity item, Point3D oldLocation, Point3D newLocation, List<UOMobileEntity> mobiles)
    {
        foreach (var mobile in mobiles)
        {
            mobile.ViewItemOnGround(item, newLocation);
        }
    }

    private void OnMobileAdded(UOMobileEntity mobile)
    {
        if (mobile.IsPlayer)
        {
            mobile.ChatMessageReceived += PlayerOnChatMessageReceived;
            mobile.ChatMessageSent += PlayerOnChatMessageSent;
            mobile.ItemOnGround += ((item, location) => PlayerItemOnGround(mobile, item, location));
            mobile.MobileMoved += ((otherMobile, location) => OnOtherMobileMoved(mobile, otherMobile, location));
            mobile.ItemRemoved += ((item, location) => PlayerItemRemoved(mobile, item, location));
            mobile.GetBackpack().ContainerItemAdded +=
                (container, itemRef) => OnContainerItemChanged(mobile, container, itemRef);
            mobile.GetBackpack().ContainerItemRemoved +=
                (container, itemRef) => OnContainerItemChanged(mobile, container, itemRef);
            mobile.EquipmentAdded += MobileOnEquipmentAdded;
            mobile.EquipmentRemoved += MobileOnEquipmentRemoved;

            return;
        }

        mobile.ChatMessageReceived += BotReceivedMessage;
        mobile.ChatMessageSent += BotSentMessage;
        mobile.MobileMoved += MobileOnMobileMoved;
    }

    private void MobileOnMobileMoved(UOMobileEntity mobile, Point3D location)
    {
    }

    private void BotSentMessage(
        UOMobileEntity? mobile, ChatMessageType messageType, short hue, string text, int graphic, int font
    )
    {
        var worldView = _spatialWorldService.GetPlayerWorldView(mobile);


        foreach (var other in worldView.NearbyMobiles)
        {
            if (other.IsPlayer)
            {
                other.ReceiveSpeech(mobile, messageType, hue, text, graphic, font);
            }
        }
    }

    private void BotReceivedMessage(
        UOMobileEntity? self, UOMobileEntity? sender, ChatMessageType messageType, short hue, string text, int graphic,
        int font
    )
    {
    }

    private void OnContainerItemChanged(UOMobileEntity mobile, UOItemEntity container, ItemReference item)
    {
        if (mobile.IsPlayer)
        {
            var session = _gameSessionService.GetGameSessionByMobile(mobile);
            if (session != null)
            {
                var containerItems = new AddMultipleItemToContainerPacket(container);
                session.SendPackets(containerItems);
            }
        }
    }

    private void MobileOnEquipmentRemoved(UOMobileEntity mobile, ItemLayerType layer, ItemReference item)
    {
        var mobileInSector = _spatialWorldService.GetPlayersInRange(
            mobile.Location,
            MapSectorConsts.MaxViewRange,
            mobile.Map.MapID
        );

        foreach (var session in mobileInSector)
        {
            var mobileDrawPacket = new MobileDrawPacket(mobile, session.Mobile, true, true);
            session.SendPackets(mobileDrawPacket);
        }
    }

    private void MobileOnEquipmentAdded(UOMobileEntity mobile, ItemLayerType layer, ItemReference item)
    {
        var mobileInSector = _spatialWorldService.GetPlayersInRange(
            mobile.Location,
            MapSectorConsts.MaxViewRange,
            mobile.Map.MapID
        );
        foreach (var session in mobileInSector)
        {
            var itemWornPacket = new WornItemPacket(mobile, item, layer);
            var mobileDrawPacket = new MobileDrawPacket(mobile, session.Mobile, true, true);
            session.SendPackets(itemWornPacket, mobileDrawPacket);
        }
    }

    private void OnOtherMobileMoved(UOMobileEntity self, UOMobileEntity otherMobile, Point3D location)
    {
        var updatePlayerPacket = new UpdatePlayerPacket(otherMobile);
        // var drawPlayer = new DrawGamePlayerPacket(otherMobile);

        var mobileDraw = new MobileDrawPacket(otherMobile, self, true, true);

        var gameSession = _gameSessionService.GetGameSessionByMobile(self);

        gameSession.SendPackets(updatePlayerPacket, mobileDraw);
    }

    private void PlayerItemOnGround(UOMobileEntity mobile, UOItemEntity item, Point3D location)
    {
        var objectInfo = new ObjectInfoPacket(item);
        var dropSound = new PlaySoundPacket(item, 0x2E1);

        var session = _gameSessionService.GetGameSessionByMobile(mobile);

        session.SendPackets(objectInfo, dropSound);
    }

    private void PlayerItemRemoved(UOMobileEntity mobile, UOItemEntity item, Point3D location)
    {
        var deleteObjectPacket = new DeleteObjectPacket(item.Id);

        var session = _gameSessionService.GetGameSessionByMobile(mobile);

        session.SendPackets(deleteObjectPacket);
    }

    private void PlayerOnChatMessageSent(
        UOMobileEntity? mobile, ChatMessageType messageType, short hue, string text, int graphic, int font
    )
    {
        _logger.Debug("Player {MobileId} sent chat message: {Message}", mobile?.Id, text);

        var messagePacket = new UnicodeSpeechResponsePacket()
        {
            Font = font,
            Graphic = graphic,
            Hue = hue,
            IsUnicode = true,
            Serial = mobile.Id,
            MessageType = messageType,
            Name = mobile?.Name ?? string.Empty,
            Text = text,
        };

        mobile.SendPackets(messagePacket);
    }

    private void PlayerOnChatMessageReceived(
        UOMobileEntity? self, UOMobileEntity? sender, ChatMessageType messageType, short hue, string text, int graphic,
        int font
    )
    {
        _logger.Debug(
            "Player {MobileId} received chat message from {SenderId}: {Message}",
            self?.Id,
            sender == null ? "System" : sender.Id,
            text
        );

        var messagePacket = new UnicodeSpeechResponsePacket()
        {
            Font = font,
            Graphic = graphic,
            Hue = hue == 0 ? (short)1310 : hue,
            IsUnicode = true,
            Serial = sender?.Id ?? Serial.Zero,
            MessageType = messageType,
            Name = sender?.Name ?? "System",
            Text = text,
        };

        self?.SendPackets(messagePacket);
    }

    private void OnMobileMoved(UOMobileEntity mobile, Point3D location, WorldView worldView)
    {
        _logger.Debug("Mobile {MobileId} moved to {Location}", mobile.Id, location);

        mobile.OtherMobileMoved(mobile, location);

        foreach (var otMobile in worldView.NearbyMobiles)
        {
            otMobile.OtherMobileMoved(mobile, location);
        }
    }

    private void OnMobileSectorMoved(UOMobileEntity mobile, MapSector oldSector, MapSector newSector)
    {
        var worldView = _spatialWorldService.GetPlayerWorldView(mobile);


        _logger.Debug(
            "Mobile {MobileId} moved from sector {OldSector} to {NewSector}",
            mobile.Id,
            oldSector,
            newSector
        );
    }

    private void OnGameSessionBeforeDestroy(GameSession session)
    {
    }

    private void OnGameSessionCreated(GameSession session)
    {
    }


    public void Dispose()
    {
    }

    private async Task HandleCommandAsync(UOMobileEntity mobile, string command)
    {
        _logger.Debug("Handling command '{Command}' for mobile {MobileId}", command, mobile.Id);

        var gameSession = _gameSessionService.QuerySessionFirstOrDefault(s => s.Mobile.Id == mobile.Id);
        await _commandSystemService.ExecuteCommandAsync(
            command,
            gameSession.SessionId,
            gameSession.Account.AccountLevel,
            CommandSourceType.InGame
        );
    }

    public void SendSystemMessageToAll(string message)
    {
        foreach (var other in _gameSessionService.QuerySessions(s => true).Select(s => s.Mobile))
        {
            // Log the system message
            _logger.Information("Sending system message to mobile {MobileId}: {Message}", other.Id, message);

            // Send the system message
            SendSystemMessageToMobile(other, message);
        }
    }

    public void SendSystemMessageToMobile(UOMobileEntity mobile, string message)
    {
        mobile.ReceiveSpeech(null, ChatMessageType.System, 0, message, 0, 3);
    }

    public async Task SendChatMessageAsync(
        UOMobileEntity mobile, ChatMessageType messageType, short hue, string text, int graphic, int font
    )
    {
        // check if command
        if (text.StartsWith("."))
        {
            await HandleCommandAsync(mobile, text[1..]);
            return;
        }

        var nonPlayersToNotify =
            _mobileService.QueryMobiles(m => m.Location.InRange(mobile.Location, UOContext.LineOfSight)).ToList();

        foreach (var other in nonPlayersToNotify)
        {
            if (other.Id == mobile.Id)
            {
                mobile.Speech(messageType, hue, text, graphic, font);
                continue;
            }

            other.ReceiveSpeech(mobile, messageType, hue, text, graphic, font);
        }
    }
}
