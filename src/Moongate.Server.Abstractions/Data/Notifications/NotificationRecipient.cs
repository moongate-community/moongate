namespace Moongate.Server.Abstractions.Data.Notifications;

/// <summary>
/// Where a notification goes: the channel that delivers it, and the address that channel understands —
/// an email address, a webhook URL, whatever the channel's own vocabulary is.
/// </summary>
public sealed record NotificationRecipient(string ChannelId, string Address);
