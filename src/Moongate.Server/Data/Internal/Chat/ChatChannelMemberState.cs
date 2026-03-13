namespace Moongate.Server.Data.Internal.Chat;

internal sealed class ChatChannelMemberState
{
    public long SessionId { get; init; }

    public bool IsModerator { get; set; }

    public bool HasVoice { get; set; }
}
