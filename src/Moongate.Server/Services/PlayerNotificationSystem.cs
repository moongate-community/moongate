using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Packets.Chat;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Services;
using Moongate.UO.Interfaces.Services.Systems;
using Serilog;

namespace Moongate.Server.Services;

public class PlayerNotificationSystem : IPlayerNotificationSystem
{
    private readonly ILogger _logger = Log.ForContext<PlayerNotificationSystem>();

    public void TrackMobile(UOMobileEntity mobile)
    {
        mobile.OtherMobileMoved += MobileOnOtherMobileMoved;
        mobile.ChatMessageSent += MobileOnChatMessageSent;
        mobile.ChatMessageReceived += MobileOnChatMessageReceived;

        mobile.GetBackpack().ContainerItemAdded += (container, item) => OnContainerItemAdded(mobile, container, item);
        mobile.GetBackpack().ContainerItemRemoved += (container, item) => OnContainerItemRemoved(mobile, container, item);
    }

    private void OnContainerItemRemoved(UOMobileEntity mobile, UOItemEntity container, ItemReference item)
    {
        var session = GetGameSessionFromMobile(mobile);

        session.SendPackets(new AddMultipleItemToContainerPacket(container));
    }

    private void OnContainerItemAdded(UOMobileEntity mobile, UOItemEntity container, ItemReference item)
    {
        var session = GetGameSessionFromMobile(mobile);

        session.SendPackets(new AddMultipleItemToContainerPacket(container));
    }

    private void MobileOnChatMessageReceived(
        UOMobileEntity self, UOMobileEntity? sender, ChatMessageType messageType, short hue, string text, int graphic,
        int font
    )
    {
        _logger.Verbose(
            "Mobile {MobileId} received chat message from {Sender}: {MessageType} - {Text}",
            self.Id,
            sender?.Id ?? Serial.MinusOne,
            messageType,
            text
        );

        var chatMessage = new UnicodeSpeechResponsePacket()
        {
            Hue = hue == 0 ? 13 : hue,
            Serial = sender?.Id ?? Serial.MinusOne,
            Name = sender?.Name ?? "System",
            Text = text,
            MessageType = messageType,
            IsUnicode = true,
            Language = "ENU",
            Font = font,
            Graphic = graphic
        };

        GetGameSessionFromMobile(self).SendPackets(chatMessage);
    }

    private void MobileOnChatMessageSent(
        UOMobileEntity? mobile, ChatMessageType messageType, short hue, string text, int graphic, int font
    )
    {
        _logger.Verbose(
            "Mobile {MobileId} sent chat message: {MessageType} - {Text}",
            mobile?.Id,
            messageType,
            text
        );
    }

    public void UntrackMobile(UOMobileEntity mobile)
    {
        mobile.OtherMobileMoved -= MobileOnOtherMobileMoved;
        mobile.ChatMessageSent -= MobileOnChatMessageSent;
        mobile.ChatMessageReceived -= MobileOnChatMessageReceived;
    }

    private void MobileOnOtherMobileMoved(UOMobileEntity mobile)
    {
        MoongateContext.EnqueueAction("PlayerNotificationSystem.MobileOnOtherMobileMoved", () => { });
    }

    private void NewPlayerJoined(UOMobileEntity mobile)
    {
        MoongateContext.EnqueueAction("PlayerNotificationSystem.NewPlayerJoined", () => { });
    }


    private GameSession GetGameSessionFromMobile(UOMobileEntity mobile)
    {
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();

        return gameSessionService.QuerySessionFirstOrDefault(s => s.Mobile.Id == mobile.Id);
    }
}
