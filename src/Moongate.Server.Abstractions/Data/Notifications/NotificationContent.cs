using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data.Notifications;

/// <summary>
/// A rendered notification, ready to deliver. <see cref="Subject" /> is null for channels that have no
/// concept of one.
/// </summary>
/// <param name="Subject">The subject line, or null when the channel has no notion of one.</param>
/// <param name="Body">The rendered body.</param>
/// <param name="ContentType">
/// How to interpret <paramref name="Body" />. Defaulted rather than required: this assembly ships as a
/// NuGet package, and a default keeps channels written against the two-parameter signature compiling.
/// </param>
public sealed record NotificationContent(
    string? Subject,
    string Body,
    NotificationContentType ContentType = NotificationContentType.Text
);
