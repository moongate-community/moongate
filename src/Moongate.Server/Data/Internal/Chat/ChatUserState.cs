using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Internal.Chat;

internal sealed class ChatUserState
{
    public long SessionId { get; init; }

    public Serial CharacterId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string CharacterName { get; init; } = string.Empty;

    public string? CurrentChannelName { get; set; }

    public bool ReceivePrivateMessages { get; set; } = true;

    public bool ShowCharacterName { get; set; } = true;

    public HashSet<long> IgnoredSessionIds { get; } = [];
}
