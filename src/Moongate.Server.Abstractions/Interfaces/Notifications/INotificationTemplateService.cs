using Moongate.Server.Abstractions.Data.Notifications;

namespace Moongate.Server.Abstractions.Interfaces.Notifications;

/// <summary>Holds the notification templates, compiled once at startup and rendered on demand.</summary>
public interface INotificationTemplateService
{
    /// <summary>How many templates are registered, across every channel.</summary>
    int Count { get; }

    /// <summary>
    /// Compiles <paramref name="source" /> and registers it for the channel. Throws
    /// <see cref="InvalidDataException" /> naming the template when the source does not compile, so a
    /// broken template is a startup failure rather than a surprise at send time.
    /// </summary>
    void Register(string channelId, string templateId, string source);

    /// <summary>
    /// Renders the template against <paramref name="model" />, or returns null when the channel has no
    /// template with that id.
    /// </summary>
    NotificationContent? Render(string channelId, string templateId, object model);
}
