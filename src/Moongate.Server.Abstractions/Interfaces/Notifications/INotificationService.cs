using Moongate.Server.Abstractions.Data.Notifications;

namespace Moongate.Server.Abstractions.Interfaces.Notifications;

/// <summary>Sends notifications. The caller says what and to whom; the channel decides how.</summary>
public interface INotificationService
{
    /// <summary>
    /// Queues a notification and returns immediately — delivery happens on a worker thread, so this is
    /// safe to call from the game loop or from a web request. Nothing about the delivery is reported
    /// back: a notification is a side effect and must never fail the operation that raised it.
    /// </summary>
    /// <param name="templateId">Template id, matching a file under the channel's template directory.</param>
    /// <param name="recipient">The channel and address to deliver to.</param>
    /// <param name="model">
    /// The data the template renders against, as an anonymous object or record. Members are exposed to
    /// the template in snake_case: <c>Username</c> is written <c>{{ username }}</c>.
    /// </param>
    void Notify(string templateId, NotificationRecipient recipient, object model);
}
