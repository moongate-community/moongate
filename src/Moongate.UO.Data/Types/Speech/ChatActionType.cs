namespace Moongate.UO.Data.Types.Speech;

public enum ChatActionType : short
{
    ChangePassword = 0x41,
    Close = 0x58,
    Message = 0x61,
    JoinConference = 0x62,
    CreateConference = 0x63,
    RenameConference = 0x64,
    SendPrivateMessage = 0x65,
    Ignore = 0x66,
    StopIgnoring = 0x67,
    ToggleIgnore = 0x68,
    GrantSpeakingPrivileges = 0x69,
    RemoveSpeakingPrivileges = 0x6A,
    ToggleSpeakingPrivileges = 0x6B,
    GrantModeratorStatus = 0x6C,
    RemoveModeratorStatus = 0x6D,
    ToggleModeratorStatus = 0x6E,
    DisablePrivateMessages = 0x6F,
    EnablePrivateMessages = 0x70,
    TogglePrivateMessages = 0x71,
    ShowCharacterName = 0x72,
    HideCharacterName = 0x73,
    ToggleShowCharacterName = 0x74,
    Whois = 0x75,
    Kick = 0x76,
    RestrictDefaultSpeakingPrivileges = 0x77,
    AllowDefaultSpeakingPrivileges = 0x78,
    ToggleDefaultSpeakingPrivileges = 0x79,
    Emote = 0x7A
}
