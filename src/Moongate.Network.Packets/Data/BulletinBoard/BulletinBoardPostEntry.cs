namespace Moongate.Network.Packets.Data.BulletinBoard;

public sealed class BulletinBoardPostEntry
{
    public string Subject { get; set; } = string.Empty;

    public List<string> BodyLines { get; } = [];
}
