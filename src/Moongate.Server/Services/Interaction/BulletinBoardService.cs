using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Data.BulletinBoard;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Interaction;

public sealed class BulletinBoardService : IBulletinBoardService
{
    private readonly ICharacterService _characterService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IItemService _itemService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IPersistenceService _persistenceService;

    public BulletinBoardService(
        IItemService itemService,
        ICharacterService characterService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IPersistenceService persistenceService
    )
    {
        _itemService = itemService;
        _characterService = characterService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _persistenceService = persistenceService;
    }

    public async Task<bool> OpenBoardAsync(long sessionId, Serial boardId)
    {
        if (!_gameNetworkSessionService.TryGet(sessionId, out _))
        {
            return false;
        }

        var board = await _itemService.GetItemAsync(boardId);

        if (!IsBulletinBoard(board))
        {
            return false;
        }

        var messages = await _persistenceService.UnitOfWork.BulletinBoardMessages.GetByBoardIdAsync(boardId);
        _outgoingPacketQueue.Enqueue(sessionId, new BulletinBoardDisplayPacket(boardId, board!.Name));

        foreach (var message in messages)
        {
            _outgoingPacketQueue.Enqueue(sessionId, CreateSummaryPacket(message));
        }

        return true;
    }

    public async Task<bool> SendSummaryAsync(GameSession session, uint boardId, uint messageId)
    {
        var message = await GetBoardMessageAsync((Serial)boardId, (Serial)messageId);

        if (message is null)
        {
            return false;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, CreateSummaryPacket(message));

        return true;
    }

    public async Task<bool> SendMessageAsync(GameSession session, uint boardId, uint messageId)
    {
        var message = await GetBoardMessageAsync((Serial)boardId, (Serial)messageId);

        if (message is null)
        {
            return false;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, CreateMessagePacket(message));

        return true;
    }

    public async Task<bool> PostMessageAsync(GameSession session, BulletinBoardMessagesPacket packet)
    {
        if (packet.Subcommand != BulletinBoardSubcommand.PostMessage)
        {
            return false;
        }

        var board = await _itemService.GetItemAsync((Serial)packet.BoardId);

        if (!IsBulletinBoard(board) || session.CharacterId == Serial.Zero)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(packet.Subject) || packet.BodyLines.Count == 0)
        {
            return false;
        }

        if (packet.ParentId != 0)
        {
            var parent = await GetBoardMessageAsync((Serial)packet.BoardId, (Serial)packet.ParentId);

            if (parent is null)
            {
                return false;
            }
        }

        var author = await ResolveAuthorAsync(session.CharacterId);
        var message = new BulletinBoardMessageEntity
        {
            MessageId = _persistenceService.UnitOfWork.AllocateNextItemId(),
            BoardId = (Serial)packet.BoardId,
            ParentId = (Serial)packet.ParentId,
            OwnerCharacterId = session.CharacterId,
            Author = author,
            Subject = packet.Subject.Trim(),
            PostedAtUtc = DateTime.UtcNow
        };
        message.BodyLines.AddRange(packet.BodyLines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.TrimEnd()));

        if (message.BodyLines.Count == 0)
        {
            return false;
        }

        await _persistenceService.UnitOfWork.BulletinBoardMessages.UpsertAsync(message);
        _outgoingPacketQueue.Enqueue(session.SessionId, CreateSummaryPacket(message));

        return true;
    }

    public async Task<bool> RemoveMessageAsync(GameSession session, uint boardId, uint messageId)
    {
        var message = await GetBoardMessageAsync((Serial)boardId, (Serial)messageId);

        if (message is null || message.OwnerCharacterId != session.CharacterId)
        {
            return false;
        }

        var boardMessages = await _persistenceService.UnitOfWork.BulletinBoardMessages.GetByBoardIdAsync((Serial)boardId);

        if (boardMessages.Any(candidate => candidate.ParentId == message.MessageId))
        {
            return false;
        }

        return await _persistenceService.UnitOfWork.BulletinBoardMessages.RemoveAsync(message.MessageId);
    }

    private async Task<BulletinBoardMessageEntity?> GetBoardMessageAsync(Serial boardId, Serial messageId)
    {
        var message = await _persistenceService.UnitOfWork.BulletinBoardMessages.GetByIdAsync(messageId);

        return message?.BoardId == boardId ? message : null;
    }

    private async Task<string> ResolveAuthorAsync(Serial characterId)
    {
        var mobile = await _characterService.GetCharacterAsync(characterId);

        return string.IsNullOrWhiteSpace(mobile?.Name) ? "Someone" : mobile.Name;
    }

    private static bool IsBulletinBoard(UOItemEntity? item)
        => item is not null &&
           (string.Equals(item.ScriptId, "items.bulletin_board", StringComparison.OrdinalIgnoreCase) ||
            item.ItemId is 0x1E5E or 0x1E5F);

    private static BulletinBoardSummaryPacket CreateSummaryPacket(BulletinBoardMessageEntity message)
        => new()
        {
            BoardId = message.BoardId,
            MessageId = message.MessageId,
            ParentId = message.ParentId,
            Poster = message.Author,
            Subject = message.Subject,
            PostedAtText = FormatPostedAt(message.PostedAtUtc)
        };

    private static BulletinBoardMessagePacket CreateMessagePacket(BulletinBoardMessageEntity message)
    {
        var packet = new BulletinBoardMessagePacket
        {
            BoardId = message.BoardId,
            MessageId = message.MessageId,
            Poster = message.Author,
            Subject = message.Subject,
            PostedAtText = FormatPostedAt(message.PostedAtUtc)
        };
        packet.BodyLines.AddRange(message.BodyLines);

        return packet;
    }

    private static string FormatPostedAt(DateTime postedAtUtc)
        => postedAtUtc.ToString("dd MMM yyyy HH:mm");
}
