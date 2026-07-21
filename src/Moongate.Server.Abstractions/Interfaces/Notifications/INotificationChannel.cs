using Moongate.Server.Abstractions.Data.Notifications;

namespace Moongate.Server.Abstractions.Interfaces.Notifications;

/// <summary>
/// One way of getting a message to a person: email, a chat webhook, the log. A channel receives text
/// that is already rendered — it knows nothing about templates.
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// The channel's id. It is also the name of the directory its templates live in, so it must be a
    /// valid directory name: lowercase, no separators.
    /// </summary>
    string Id { get; }

    /// <summary>Delivers the rendered notification. Throwing signals a failure worth retrying.</summary>
    ValueTask SendAsync(
        NotificationRecipient recipient,
        NotificationContent content,
        CancellationToken cancellationToken = default
    );
}
