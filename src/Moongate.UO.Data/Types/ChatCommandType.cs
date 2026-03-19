namespace Moongate.UO.Data.Types;

/// <summary>
/// Chat command codes used by packet 0xB2.
/// </summary>
public enum ChatCommandType : ushort
{
    AddChannel = 0x03E8,
    RemoveChannel = 0x03E9,
    AskNewNickname = 0x03EB,
    CloseChatWindow = 0x03EC,
    OpenChatWindow = 0x03ED,
    AddUserToChannel = 0x03EE,
    RemoveUserFromChannel = 0x03EF,
    LeaveChannel = 0x03F0,
    JoinedChannel = 0x03F1
}
