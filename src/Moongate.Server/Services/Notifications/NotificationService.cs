using Moongate.Core.Extensions;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Notifications;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Serilog;
using SquidStd.Core.Interfaces.Jobs;

namespace Moongate.Server.Services.Notifications;

/// <summary>
/// Routes a notification to its channel and delivers it on a worker thread, so neither the game loop nor
/// a web request ever waits on a transport. Every failure ends here: a notification is a side effect and
/// must not reach the code that raised it.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly ILogger _logger = Log.ForContext<NotificationService>();
    private readonly INotificationTemplateService _templates;
    private readonly IReadOnlyDictionary<string, INotificationChannel> _channels;
    private readonly IJobSystem _jobs;
    private readonly NotificationConfig _config;

    public NotificationService(
        INotificationTemplateService templates,
        IEnumerable<INotificationChannel> channels,
        IJobSystem jobs,
        NotificationConfig config
    )
    {
        _templates = templates;
        _channels = channels.ToDictionary(channel => channel.Id, StringComparer.OrdinalIgnoreCase);
        _jobs = jobs;
        _config = config;
    }

    public void Notify(string templateId, NotificationRecipient recipient, object model)
    {
        if (!_channels.TryGetValue(recipient.ChannelId, out var channel))
        {
            _logger.Warning(
                "No notification channel '{Channel}' is registered; dropping '{Template}'",
                recipient.ChannelId,
                templateId
            );

            return;
        }

        // Deliberately not awaited: the caller may be the game loop, and delivery is best-effort.
        _ = _jobs.ScheduleAsync(() => Deliver(channel, templateId, recipient, model));
    }

    private void Deliver(
        INotificationChannel channel,
        string templateId,
        NotificationRecipient recipient,
        object model
    )
    {
        if (Render(channel, templateId, model) is not { } content)
        {
            return;
        }

        for (var attempt = 1; attempt <= _config.MaxAttempts; attempt++)
        {
            try
            {
                channel.SendAsync(recipient, content).WaitSync();

                return;
            }
            catch (Exception exception)
            {
                if (attempt >= _config.MaxAttempts)
                {
                    _logger.Error(
                        exception,
                        "Gave up delivering '{Template}' to {Address} on '{Channel}' after {Attempts} attempt(s)",
                        templateId,
                        recipient.Address,
                        channel.Id,
                        attempt
                    );

                    return;
                }

                _logger.Warning(
                    exception,
                    "Attempt {Attempt} to deliver '{Template}' on '{Channel}' failed; retrying",
                    attempt,
                    templateId,
                    channel.Id
                );

                // Blocking a worker thread, not the loop: the job system exists for exactly this.
                Thread.Sleep(TimeSpan.FromSeconds(_config.RetryDelaySeconds));
            }
        }
    }

    private NotificationContent? Render(INotificationChannel channel, string templateId, object model)
    {
        try
        {
            if (_templates.Render(channel.Id, templateId, model) is { } content)
            {
                return content;
            }

            _logger.Warning(
                "No '{Template}' template for channel '{Channel}'; dropping",
                templateId,
                channel.Id
            );
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Rendering '{Template}' for channel '{Channel}' failed; dropping",
                templateId,
                channel.Id
            );
        }

        return null;
    }
}
