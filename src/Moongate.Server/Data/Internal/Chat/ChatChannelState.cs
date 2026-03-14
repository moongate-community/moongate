namespace Moongate.Server.Data.Internal.Chat;

internal sealed class ChatChannelState
{
    public string Name { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool VoiceRestricted { get; set; }

    public Dictionary<long, ChatChannelMemberState> Members { get; } = [];
}
