using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Connections;
using Moongate.Server.Data.Internal.Chat;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Types.Speech;

namespace Moongate.Server.Services.Speech;

[RegisterGameEventListener]
public sealed class ChatSystemService : IChatSystemService, IGameEventListener<PlayerDisconnectedEvent>
{
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly Dictionary<long, ChatUserState> _usersBySessionId = [];
    private readonly Dictionary<string, ChatChannelState> _channelsByName = new(StringComparer.OrdinalIgnoreCase);

    public ChatSystemService(IOutgoingPacketQueue outgoingPacketQueue, IGameNetworkSessionService gameNetworkSessionService)
    {
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public Task OpenWindowAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        var user = GetOrCreateUser(session);

        EnqueueCommand(session.SessionId, ChatCommandType.OpenChatWindow, user.Username);

        foreach (var channel in _channelsByName.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            EnqueueCommand(session.SessionId, ChatCommandType.AddChannel, channel.Name, "0");
        }

        return Task.CompletedTask;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    public Task HandleChatActionAsync(GameSession session, ChatTextPacket packet, CancellationToken cancellationToken = default)
    {
        var user = GetOrCreateUser(session);

        return HandleActionAsync(user, packet.ActionId, packet.Payload);
    }

    public Task RemoveSessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        if (!_usersBySessionId.TryGetValue(sessionId, out var user))
        {
            return Task.CompletedTask;
        }

        RemoveUser(user, closeWindow: false);

        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerDisconnectedEvent gameEvent, CancellationToken cancellationToken = default)
        => RemoveSessionAsync(gameEvent.SessionId, cancellationToken);

    private Task HandleActionAsync(ChatUserState user, ChatActionType actionId, string param)
    {
        switch (actionId)
        {
            case ChatActionType.ChangePassword:
                if (!TryRequireModerator(user, out var passwordChannel))
                {
                    return Task.CompletedTask;
                }

                passwordChannel.Password = NormalizeOptional(param);
                SendSystem(user.SessionId, 60);
                break;

            case ChatActionType.Close:
                RemoveUser(user, closeWindow: true);
                break;

            case ChatActionType.Message:
                if (!TryRequireConference(user, out var messageChannel))
                {
                    return Task.CompletedTask;
                }

                if (!CanTalk(messageChannel, user.SessionId))
                {
                    SendSystem(user.SessionId, 36);
                    return Task.CompletedTask;
                }

                BroadcastIgnorable(messageChannel, 57, user, GetColorizedUsername(user, messageChannel), param);
                break;

            case ChatActionType.JoinConference:
                JoinExistingChannel(user, param);
                break;

            case ChatActionType.CreateConference:
                CreateOrJoinChannel(user, param);
                break;

            case ChatActionType.RenameConference:
                if (!TryRequireModerator(user, out var renameChannel))
                {
                    return Task.CompletedTask;
                }

                RenameChannel(renameChannel, NormalizeRequired(param));
                break;

            case ChatActionType.SendPrivateMessage:
                SendPrivateMessage(user, param);
                break;

            case ChatActionType.Ignore:
                AddIgnore(user, param);
                break;

            case ChatActionType.StopIgnoring:
                RemoveIgnore(user, param);
                break;

            case ChatActionType.ToggleIgnore:
                ToggleIgnore(user, param);
                break;

            case ChatActionType.GrantSpeakingPrivileges:
                UpdateVoice(user, param, VoiceMutation.Add);
                break;

            case ChatActionType.RemoveSpeakingPrivileges:
                UpdateVoice(user, param, VoiceMutation.Remove);
                break;

            case ChatActionType.ToggleSpeakingPrivileges:
                UpdateVoice(user, param, VoiceMutation.Toggle);
                break;

            case ChatActionType.GrantModeratorStatus:
                UpdateModerator(user, param, MembershipMutation.Add);
                break;

            case ChatActionType.RemoveModeratorStatus:
                UpdateModerator(user, param, MembershipMutation.Remove);
                break;

            case ChatActionType.ToggleModeratorStatus:
                UpdateModerator(user, param, MembershipMutation.Toggle);
                break;

            case ChatActionType.DisablePrivateMessages:
                user.ReceivePrivateMessages = false;
                SendSystem(user.SessionId, 38);
                break;

            case ChatActionType.EnablePrivateMessages:
                user.ReceivePrivateMessages = true;
                SendSystem(user.SessionId, 37);
                break;

            case ChatActionType.TogglePrivateMessages:
                user.ReceivePrivateMessages = !user.ReceivePrivateMessages;
                SendSystem(user.SessionId, (ushort)(user.ReceivePrivateMessages ? 37 : 38));
                break;

            case ChatActionType.ShowCharacterName:
                user.ShowCharacterName = true;
                SendSystem(user.SessionId, 39);
                break;

            case ChatActionType.HideCharacterName:
                user.ShowCharacterName = false;
                SendSystem(user.SessionId, 40);
                break;

            case ChatActionType.ToggleShowCharacterName:
                user.ShowCharacterName = !user.ShowCharacterName;
                SendSystem(user.SessionId, (ushort)(user.ShowCharacterName ? 39 : 40));
                break;

            case ChatActionType.Whois:
                QueryWhoIs(user, param);
                break;

            case ChatActionType.Kick:
                Kick(user, param);
                break;

            case ChatActionType.RestrictDefaultSpeakingPrivileges:
                SetDefaultVoiceRestricted(user, true);
                break;

            case ChatActionType.AllowDefaultSpeakingPrivileges:
                SetDefaultVoiceRestricted(user, false);
                break;

            case ChatActionType.ToggleDefaultSpeakingPrivileges:
                ToggleDefaultVoiceRestricted(user);
                break;

            case ChatActionType.Emote:
                if (!TryRequireConference(user, out var emoteChannel))
                {
                    return Task.CompletedTask;
                }

                if (!CanTalk(emoteChannel, user.SessionId))
                {
                    SendSystem(user.SessionId, 36);
                    return Task.CompletedTask;
                }

                BroadcastIgnorable(emoteChannel, 58, user, GetColorizedUsername(user, emoteChannel), param);
                break;
        }

        return Task.CompletedTask;
    }

    private void JoinExistingChannel(ChatUserState user, string param)
    {
        ParseJoinChannel(param, out var name, out var password);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!_channelsByName.TryGetValue(name, out var channel))
        {
            SendSystem(user.SessionId, 33, name);
            return;
        }

        if (!ValidatePassword(channel, password))
        {
            SendSystem(user.SessionId, 34);
            return;
        }

        AddUserToChannel(channel, user);
    }

    private void CreateOrJoinChannel(ChatUserState user, string param)
    {
        ParseCreateChannel(param, out var name, out var password);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!_channelsByName.TryGetValue(name, out var channel))
        {
            channel = new ChatChannelState
            {
                Name = name,
                Password = NormalizeOptional(password)
            };
            _channelsByName[name] = channel;
            BroadcastCommand(ChatCommandType.AddChannel, channel.Name, "0");
        }

        AddUserToChannel(channel, user, NormalizeOptional(password));
    }

    private void AddIgnore(ChatUserState user, string param)
    {
        var target = FindUserByName(param);

        if (target is null)
        {
            return;
        }

        if (!user.IgnoredSessionIds.Add(target.SessionId))
        {
            SendSystem(user.SessionId, 22, target.Username);
            return;
        }

        SendSystem(user.SessionId, 23, target.Username);
    }

    private void RemoveIgnore(ChatUserState user, string param)
    {
        var target = FindUserByName(param);

        if (target is null)
        {
            return;
        }

        if (!user.IgnoredSessionIds.Remove(target.SessionId))
        {
            SendSystem(user.SessionId, 25, target.Username);
            return;
        }

        SendSystem(user.SessionId, 24, target.Username);

        if (user.IgnoredSessionIds.Count == 0)
        {
            SendSystem(user.SessionId, 26);
        }
    }

    private void ToggleIgnore(ChatUserState user, string param)
    {
        var target = FindUserByName(param);

        if (target is null)
        {
            return;
        }

        if (user.IgnoredSessionIds.Contains(target.SessionId))
        {
            RemoveIgnore(user, target.Username);
        }
        else
        {
            AddIgnore(user, target.Username);
        }
    }

    private void SendPrivateMessage(ChatUserState from, string param)
    {
        var separator = param.IndexOf(' ');

        if (separator <= 0 || separator >= param.Length - 1)
        {
            return;
        }

        var targetName = param[..separator].Trim();
        var text = param[(separator + 1)..].Trim();
        var target = FindUserByName(targetName);

        if (target is null)
        {
            return;
        }

        if (target.IgnoredSessionIds.Contains(from.SessionId))
        {
            SendSystem(from.SessionId, 35, target.Username);
            return;
        }

        if (!target.ReceivePrivateMessages)
        {
            SendSystem(from.SessionId, 42, target.Username);
            return;
        }

        var currentChannel = TryGetCurrentChannel(from, out var channel) ? channel : null;
        SendSystem(target.SessionId, 59, GetColorizedUsername(from, currentChannel), text);
    }

    private void UpdateVoice(ChatUserState user, string param, VoiceMutation mutation)
    {
        if (!TryRequireModerator(user, out var channel))
        {
            return;
        }

        var target = FindMemberInChannel(channel, param);

        if (target is null)
        {
            return;
        }

        var member = channel.Members[target.SessionId];

        switch (mutation)
        {
            case VoiceMutation.Add:
                member.HasVoice = true;
                SendSystem(target.SessionId, 54, user.Username);
                SendSystemToChannel(channel, 52, target.SessionId, target.Username);
                break;
            case VoiceMutation.Remove:
                member.HasVoice = false;
                SendSystem(target.SessionId, 53, user.Username);
                SendSystemToChannel(channel, 51, target.SessionId, target.Username);
                break;
            case VoiceMutation.Toggle:
                member.HasVoice = !member.HasVoice;
                SendSystem(target.SessionId, (ushort)(member.HasVoice ? 54 : 53), user.Username);
                SendSystemToChannel(channel, (ushort)(member.HasVoice ? 52 : 51), target.SessionId, target.Username);
                break;
        }

        BroadcastMemberRefresh(channel, target);
    }

    private void UpdateModerator(ChatUserState user, string param, MembershipMutation mutation)
    {
        if (!TryRequireModerator(user, out var channel))
        {
            return;
        }

        var target = FindMemberInChannel(channel, param);

        if (target is null)
        {
            return;
        }

        var member = channel.Members[target.SessionId];

        switch (mutation)
        {
            case MembershipMutation.Add:
                member.IsModerator = true;
                member.HasVoice = false;
                SendSystem(target.SessionId, 50, user.Username);
                SendSystemToChannel(channel, 48, target.SessionId, target.Username);
                break;
            case MembershipMutation.Remove:
                member.IsModerator = false;
                SendSystem(target.SessionId, 49, user.Username);
                SendSystemToChannel(channel, 47, target.SessionId, target.Username);
                break;
            case MembershipMutation.Toggle:
                member.IsModerator = !member.IsModerator;
                if (member.IsModerator)
                {
                    member.HasVoice = false;
                    SendSystem(target.SessionId, 50, user.Username);
                    SendSystemToChannel(channel, 48, target.SessionId, target.Username);
                }
                else
                {
                    SendSystem(target.SessionId, 49, user.Username);
                    SendSystemToChannel(channel, 47, target.SessionId, target.Username);
                }

                break;
        }

        BroadcastMemberRefresh(channel, target);
    }

    private void QueryWhoIs(ChatUserState from, string param)
    {
        var target = FindUserByName(param);

        if (target is null)
        {
            return;
        }

        if (!target.ShowCharacterName)
        {
            SendSystem(from.SessionId, 41, target.Username);
            return;
        }

        SendSystem(from.SessionId, 43, target.Username, target.CharacterName);
    }

    private void Kick(ChatUserState from, string param)
    {
        if (!TryRequireModerator(from, out var channel))
        {
            return;
        }

        var target = FindMemberInChannel(channel, param);

        if (target is null)
        {
            return;
        }

        SendSystem(target.SessionId, 45, from.Username);
        RemoveUserFromChannel(channel, target);
        SendSystemToChannel(channel, 44, target.SessionId, target.Username);
    }

    private void SetDefaultVoiceRestricted(ChatUserState user, bool restricted)
    {
        if (!TryRequireModerator(user, out var channel))
        {
            return;
        }

        channel.VoiceRestricted = restricted;
        SendSystemToChannel(channel, (ushort)(restricted ? 56 : 55));
    }

    private void ToggleDefaultVoiceRestricted(ChatUserState user)
    {
        if (!TryRequireModerator(user, out var channel))
        {
            return;
        }

        channel.VoiceRestricted = !channel.VoiceRestricted;
        SendSystemToChannel(channel, (ushort)(channel.VoiceRestricted ? 56 : 55));
    }

    private ChatUserState GetOrCreateUser(GameSession session)
    {
        if (_usersBySessionId.TryGetValue(session.SessionId, out var existing))
        {
            return existing;
        }

        var username = string.IsNullOrWhiteSpace(session.Character?.Name) ? $"User{session.SessionId}" : session.Character.Name;
        var user = new ChatUserState
        {
            SessionId = session.SessionId,
            CharacterId = session.CharacterId,
            Username = username,
            CharacterName = session.Character?.Name ?? username
        };
        _usersBySessionId[session.SessionId] = user;

        return user;
    }

    private void AddUserToChannel(ChatChannelState channel, ChatUserState user, string? password = null)
    {
        if (channel.Members.ContainsKey(user.SessionId))
        {
            SendSystem(user.SessionId, 46, channel.Name);
            return;
        }

        if (!ValidatePassword(channel, password))
        {
            SendSystem(user.SessionId, 34);
            return;
        }

        if (TryGetCurrentChannel(user, out var current))
        {
            RemoveUserFromChannel(current, user);
        }

        var isFirstMember = channel.Members.Count == 0;
        channel.Members[user.SessionId] = new ChatChannelMemberState
        {
            SessionId = user.SessionId,
            IsModerator = isFirstMember,
            HasVoice = false
        };
        user.CurrentChannelName = channel.Name;

        EnqueueCommand(user.SessionId, ChatCommandType.JoinedChannel, channel.Name);
        BroadcastCommandToChannel(channel, ChatCommandType.AddUserToChannel, null, GetColorizedUsername(user, channel));

        foreach (var member in channel.Members.Keys.ToArray())
        {
            var existingUser = _usersBySessionId[member];
            EnqueueCommand(user.SessionId, ChatCommandType.AddUserToChannel, GetColorizedUsername(existingUser, channel));
        }
    }

    private void RemoveUser(ChatUserState user, bool closeWindow)
    {
        if (TryGetCurrentChannel(user, out var channel))
        {
            RemoveUserFromChannel(channel, user);
        }

        foreach (var other in _usersBySessionId.Values)
        {
            if (other.SessionId != user.SessionId)
            {
                other.IgnoredSessionIds.Remove(user.SessionId);
            }
        }

        _usersBySessionId.Remove(user.SessionId);

        if (closeWindow)
        {
            EnqueueCommand(user.SessionId, ChatCommandType.CloseChatWindow);
        }
    }

    private void RemoveUserFromChannel(ChatChannelState channel, ChatUserState user)
    {
        if (!channel.Members.Remove(user.SessionId))
        {
            return;
        }

        user.CurrentChannelName = null;
        BroadcastCommandToChannel(channel, ChatCommandType.RemoveUserFromChannel, null, user.Username);
        EnqueueCommand(user.SessionId, ChatCommandType.LeaveChannel);

        if (channel.Members.Count == 0)
        {
            _channelsByName.Remove(channel.Name);
            BroadcastCommand(ChatCommandType.RemoveChannel, channel.Name);
        }
    }

    private void RenameChannel(ChatChannelState channel, string newName)
    {
        if (string.Equals(channel.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _channelsByName.Remove(channel.Name);
        BroadcastCommand(ChatCommandType.RemoveChannel, channel.Name);
        channel.Name = newName;
        _channelsByName[channel.Name] = channel;
        BroadcastCommand(ChatCommandType.AddChannel, channel.Name, "0");

        foreach (var member in channel.Members.Keys)
        {
            _usersBySessionId[member].CurrentChannelName = channel.Name;
            EnqueueCommand(member, ChatCommandType.JoinedChannel, channel.Name);
        }
    }

    private void BroadcastMemberRefresh(ChatChannelState channel, ChatUserState target)
    {
        BroadcastCommandToChannel(channel, ChatCommandType.AddUserToChannel, null, GetColorizedUsername(target, channel));
    }

    private void BroadcastIgnorable(ChatChannelState channel, ushort number, ChatUserState from, string param1, string param2)
    {
        foreach (var member in channel.Members.Keys)
        {
            if (_usersBySessionId.TryGetValue(member, out var recipient) && !recipient.IgnoredSessionIds.Contains(from.SessionId))
            {
                SendSystem(member, number, param1, param2);
            }
        }
    }

    private void SendSystemToChannel(ChatChannelState channel, ushort number, long? initiatorSessionId = null, string? param1 = null, string? param2 = null)
    {
        foreach (var member in channel.Members.Keys)
        {
            if (initiatorSessionId.HasValue && member == initiatorSessionId.Value)
            {
                continue;
            }

            SendSystem(member, number, param1, param2);
        }
    }

    private void BroadcastCommand(ChatCommandType command, string param1 = "", string param2 = "")
    {
        foreach (var user in _usersBySessionId.Values)
        {
            EnqueueCommand(user.SessionId, command, param1, param2);
        }
    }

    private void BroadcastCommandToChannel(ChatChannelState channel, ChatCommandType command, long? initiatorSessionId, string param1 = "", string param2 = "")
    {
        foreach (var member in channel.Members.Keys)
        {
            if (initiatorSessionId.HasValue && member == initiatorSessionId.Value)
            {
                continue;
            }

            EnqueueCommand(member, command, param1, param2);
        }
    }

    private void EnqueueCommand(long sessionId, ChatCommandType command, string param1 = "", string param2 = "")
        => _outgoingPacketQueue.Enqueue(sessionId, new ChatCommandPacket(command, param1, param2));

    private void SendSystem(long sessionId, ushort number, string param1 = "", string param2 = "")
        => _outgoingPacketQueue.Enqueue(sessionId, new ChatCommandPacket(number, param1, param2));

    private bool TryGetCurrentChannel(ChatUserState user, out ChatChannelState channel)
    {
        if (user.CurrentChannelName is not null && _channelsByName.TryGetValue(user.CurrentChannelName, out channel!))
        {
            return true;
        }

        channel = null!;

        return false;
    }

    private bool TryRequireConference(ChatUserState user, out ChatChannelState channel)
    {
        if (TryGetCurrentChannel(user, out channel))
        {
            return true;
        }

        SendSystem(user.SessionId, 31);

        return false;
    }

    private bool TryRequireModerator(ChatUserState user, out ChatChannelState channel)
    {
        if (!TryRequireConference(user, out channel))
        {
            return false;
        }

        if (channel.Members.TryGetValue(user.SessionId, out var membership) && membership.IsModerator)
        {
            return true;
        }

        SendSystem(user.SessionId, 29);

        return false;
    }

    private bool CanTalk(ChatChannelState channel, long sessionId)
    {
        if (!channel.Members.TryGetValue(sessionId, out var member))
        {
            return false;
        }

        return !channel.VoiceRestricted || member.IsModerator || member.HasVoice;
    }

    private bool ValidatePassword(ChatChannelState channel, string? password)
        => string.IsNullOrWhiteSpace(channel.Password) ||
           string.Equals(channel.Password, NormalizeOptional(password), StringComparison.OrdinalIgnoreCase);

    private ChatUserState? FindUserByName(string username)
        => _usersBySessionId.Values.FirstOrDefault(x => string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

    private ChatUserState? FindMemberInChannel(ChatChannelState channel, string username)
    {
        var target = FindUserByName(username);

        return target is not null && channel.Members.ContainsKey(target.SessionId) ? target : null;
    }

    private static string GetColorizedUsername(ChatUserState user, ChatChannelState? channel)
    {
        if (channel is not null && channel.Members.TryGetValue(user.SessionId, out var member))
        {
            if (member.IsModerator)
            {
                return $"1{user.Username}";
            }

            if (member.HasVoice)
            {
                return $"2{user.Username}";
            }
        }

        return $"0{user.Username}";
    }

    private static string NormalizeRequired(string value)
        => value.Trim();

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static void ParseJoinChannel(string param, out string name, out string? password)
    {
        name = param.Trim();
        password = null;

        var start = param.IndexOf('\"');

        if (start >= 0)
        {
            var end = param.IndexOf('\"', start + 1);

            if (end > start)
            {
                name = param.Substring(start + 1, end - start - 1).Trim();
                password = NormalizeOptional(param[(end + 1)..]);
                return;
            }
        }

        var separator = param.IndexOf(' ');

        if (separator > 0)
        {
            name = param[..separator].Trim();
            password = NormalizeOptional(param[(separator + 1)..]);
        }
    }

    private static void ParseCreateChannel(string param, out string name, out string? password)
    {
        password = null;
        param = param.Trim();
        var start = param.IndexOf('{');

        if (start >= 0)
        {
            name = param[..start].Trim();
            var end = param.IndexOf('}', start + 1);
            password = end > start ? NormalizeOptional(param.Substring(start + 1, end - start - 1)) : null;

            return;
        }

        name = param.Trim();
    }

    private enum VoiceMutation
    {
        Add,
        Remove,
        Toggle
    }

    private enum MembershipMutation
    {
        Add,
        Remove,
        Toggle
    }
}
