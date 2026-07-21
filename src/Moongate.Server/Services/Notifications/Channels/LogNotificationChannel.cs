using Moongate.Server.Abstractions.Data.Notifications;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Serilog;

namespace Moongate.Server.Services.Notifications.Channels;

/// <summary>
/// Writes notifications to the server log. It is the channel that always works, which makes it the one
/// a shard falls back on before it has configured a real transport.
/// </summary>
public sealed class LogNotificationChannel : INotificationChannel
{
    private readonly ILogger _logger = Log.ForContext<LogNotificationChannel>();

    public string Id => "log";

    public ValueTask SendAsync(
        NotificationRecipient recipient,
        NotificationContent content,
        CancellationToken cancellationToken = default
    )
    {
        if (content.Subject is { } subject)
        {
            _logger.Information(
                "Notification to {Address} — {Subject}{NewLine}{Body}",
                recipient.Address,
                subject,
                Environment.NewLine,
                content.Body
            );
        }
        else
        {
            _logger.Information(
                "Notification to {Address}{NewLine}{Body}",
                recipient.Address,
                Environment.NewLine,
                content.Body
            );
        }

        return ValueTask.CompletedTask;
    }
}
