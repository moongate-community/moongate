using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Server.Abstractions.Interfaces.Server;
using Serilog;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Turns a pending web registration into a verification notification, on whichever channel
/// <see cref="NotificationConfig.AccountVerificationChannel" /> names.
/// </summary>
public sealed class AccountRegistrationSubscriber : IEventSubscriberRegistration
{
    private const string TemplateId = "account_verification";

    private readonly ILogger _logger = Log.ForContext<AccountRegistrationSubscriber>();
    private readonly INotificationService _notifications;
    private readonly IReadOnlyCollection<string> _channelIds;
    private readonly IServerSettingsService _settings;
    private readonly MoongateConfig _config;
    private readonly string _channelId;

    public AccountRegistrationSubscriber(
        INotificationService notifications,
        IEnumerable<INotificationChannel> channels,
        IServerSettingsService settings,
        MoongateConfig config,
        NotificationConfig notificationConfig
    )
    {
        _notifications = notifications;
        _channelIds = [.. channels.Select(channel => channel.Id)];
        _settings = settings;
        _config = config;
        _channelId = notificationConfig.AccountVerificationChannel;
    }

    public void Subscribe(IEventBus eventBus)
    {
        // Said out loud at startup, because the alternative is an operator installing a transport,
        // forgetting to point the config at it, and finding out only when a player cannot register.
        if (_channelIds.Contains(_channelId, StringComparer.OrdinalIgnoreCase))
        {
            _logger.Information("Account verification will be delivered on the '{Channel}' channel", _channelId);
        }
        else
        {
            _logger.Warning(
                "Account verification is routed to the '{Channel}' channel, which is not registered; verification messages will be dropped. Registered channels: {Channels}",
                _channelId,
                _channelIds
            );
        }

        eventBus.Subscribe<AccountRegistrationRequestedEvent>(OnRegistrationRequested);
    }

    private Task OnRegistrationRequested(
        AccountRegistrationRequestedEvent message,
        CancellationToken cancellationToken
    )
    {
        // The verify URL is composed by the template, not here: its shape belongs to the website, and
        // changing it should cost an edit rather than a release.
        _notifications.Notify(
            TemplateId,
            new(_channelId, message.Email),
            new
            {
                message.Username,
                message.Email,
                message.Token,
                Website = _settings.Get().Contacts.Website ?? string.Empty,
                _config.ShardName
            }
        );

        return Task.CompletedTask;
    }
}
