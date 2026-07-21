namespace Moongate.Server.Abstractions.Data.Notifications;

/// <summary>
/// A rendered notification, ready to deliver. <see cref="Subject" /> is null for channels that have no
/// concept of one.
/// </summary>
public sealed record NotificationContent(string? Subject, string Body);
