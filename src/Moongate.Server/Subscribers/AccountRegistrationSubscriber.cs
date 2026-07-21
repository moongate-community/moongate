using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Server.Abstractions.Interfaces.Server;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>
/// Turns a pending web registration into a verification notification. Until an email channel exists this
/// goes to the log, which is what makes the token reachable on a shard with no transport configured.
/// </summary>
public sealed class AccountRegistrationSubscriber : IEventSubscriberRegistration
{
    private const string TemplateId = "account_verification";
    private const string ChannelId = "log";

    private readonly INotificationService _notifications;
    private readonly IServerSettingsService _settings;
    private readonly MoongateConfig _config;

    public AccountRegistrationSubscriber(
        INotificationService notifications,
        IServerSettingsService settings,
        MoongateConfig config
    )
    {
        _notifications = notifications;
        _settings = settings;
        _config = config;
    }

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<AccountRegistrationRequestedEvent>(OnRegistrationRequested);

    private Task OnRegistrationRequested(
        AccountRegistrationRequestedEvent message,
        CancellationToken cancellationToken
    )
    {
        // The verify URL is composed by the template, not here: its shape belongs to the website, and
        // changing it should cost an edit rather than a release.
        _notifications.Notify(
            TemplateId,
            new(ChannelId, message.Email),
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
