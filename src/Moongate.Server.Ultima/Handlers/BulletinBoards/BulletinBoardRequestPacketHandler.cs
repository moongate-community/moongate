using System.Globalization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.BulletinBoards;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.BulletinBoards;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.BulletinBoards;

namespace Moongate.Server.Ultima.Handlers.BulletinBoards;

/// <summary>
///     Answers what a client asks of a bulletin board (0x71): the summary or the text of a message, a post, a
///     removal. Every request is checked again: the item is a board, the character stands within two tiles of it on
///     its map, and the message asked for is one of that board. What fails is dropped without a word.
/// </summary>
public sealed class BulletinBoardRequestPacketHandler : IPacketHandler<BulletinBoardRequestPacket>
{
    /// <summary>
    ///     How far from a board a character reads and posts, in tiles.
    /// </summary>
    public const int Range = 2;

    private readonly IBulletinBoardService _boards;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;
    private readonly ISpeechService _speech;
    private readonly ILocalizationService? _localization;

    public BulletinBoardRequestPacketHandler(
        IBulletinBoardService boards,
        IItemService items,
        IMobileService mobiles,
        IPacketSendService sender,
        ISpeechService speech,
        ILocalizationService? localization = null
    )
    {
        _boards = boards;
        _items = items;
        _mobiles = mobiles;
        _sender = sender;
        _speech = speech;
        _localization = localization;
    }

    public void Handle(GameSession session, BulletinBoardRequestPacket packet)
    {
        if (!session.CharacterId.IsValid ||
            !_mobiles.TryGet(session.CharacterId, out var character) ||
            !_items.TryGet(packet.Board, out var board) ||
            !_boards.IsBoard(board) ||
            !Reaches(session, character, board))
        {
            return;
        }

        if (packet.Command == BulletinBoardCommandType.Post)
        {
            Post(session, character, board, packet);

            return;
        }

        // A message of another board is not read or removed through this one.
        if (_boards.GetMessage(packet.Message) is not { } message || message.BoardId != board.Id)
        {
            return;
        }

        switch (packet.Command)
        {
            case BulletinBoardCommandType.RequestSummary:
                _sender.TrySend(
                    session.SessionId,
                    new BulletinBoardSummaryPacket(
                        board.Id,
                        message.Id,
                        message.ThreadId,
                        message.PosterName,
                        message.Subject,
                        Date(message.PostedAt)
                    )
                );

                break;
            case BulletinBoardCommandType.RequestMessage:
                _sender.TrySend(
                    session.SessionId,
                    new BulletinBoardMessagePacket(
                        board.Id,
                        message.Id,
                        message.PosterName,
                        message.Subject,
                        Date(message.PostedAt),
                        message.PosterBody,
                        message.PosterHue,
                        BulletinEquipment.Parse(message.PosterEquipment),
                        message.Lines()
                    )
                );

                break;
            case BulletinBoardCommandType.Remove:
                Remove(session, character, message);

                break;
        }
    }

    private void Post(GameSession session, MobileEntity character, ItemEntity board, BulletinBoardRequestPacket packet)
    {
        var result = _boards.Post(board, character, session.AccountType, packet.Message, packet.Subject, packet.Lines);

        switch (result.Type)
        {
            case BulletinPostResultType.TooSoon:
                _speech.Tell(
                    character,
                    _localization.Text(
                        BulletinBoardService.WaitMessage,
                        "You must wait {0} seconds before posting again.",
                        result.WaitSeconds
                    )
                );

                break;
            case BulletinPostResultType.Busy:
                _speech.Tell(
                    character,
                    _localization.Text(BulletinBoardService.BusyMessage, "The board is busy: post again in a moment.")
                );

                break;
            case BulletinPostResultType.Ok:
                // What a full board let go leaves the poster's list, and the new message enters it.
                foreach (var dropped in result.Dropped)
                {
                    _sender.TrySend(session.SessionId, new RemoveEntityPacket(dropped));
                }

                // Safe: a successful result always carries the message.
                _sender.TrySend(
                    session.SessionId,
                    new ContainerItemUpdatePacket(BulletinBoardService.AsItem(result.Message!), session.UsesContainerGrid())
                );

                break;
        }
    }

    private void Remove(GameSession session, MobileEntity character, BulletinMessageEntity message)
    {
        if (!_boards.CanRemove(message, character, session.AccountType))
        {
            _speech.Tell(character, _localization.Text(BulletinBoardService.NotYoursMessage, "That message is not yours."));

            return;
        }

        foreach (var gone in _boards.Remove(message.Id))
        {
            _sender.TrySend(session.SessionId, new RemoveEntityPacket(gone));
        }
    }

    // Within two tiles of a board lying on the character's map; the staff reads from anywhere.
    private static bool Reaches(GameSession session, MobileEntity character, ItemEntity board)
    {
        if (session.AccountType >= AccountType.GameMaster)
        {
            return true;
        }

        return board.GroundLocation is { } place &&
               board.Map == character.Map &&
               Math.Abs(place.X - character.Location.X) <= Range &&
               Math.Abs(place.Y - character.Location.Y) <= Range;
    }

    // "Oct 05, 2026": the day only, in English whatever the culture of the server.
    private static string Date(long milliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
    }
}
