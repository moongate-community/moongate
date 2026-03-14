namespace Moongate.Network.Packets.Data.BulletinBoard;

public enum BulletinBoardSubcommand : byte
{
    DisplayBulletinBoard = 0,
    MessageSummary = 1,
    Message = 2,
    RequestMessage = 3,
    RequestMessageSummary = 4,
    PostMessage = 5,
    RemovePostedMessage = 6
}
