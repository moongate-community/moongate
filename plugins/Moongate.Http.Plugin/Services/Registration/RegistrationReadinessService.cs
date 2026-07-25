using Moongate.Http.Plugin.Data.Registration;
using Moongate.Http.Plugin.Interfaces.Registration;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Notifications;

namespace Moongate.Http.Plugin.Services.Registration;

/// <summary>Evaluates the local configuration prerequisites for public account registration.</summary>
public sealed class RegistrationReadinessService : IRegistrationReadinessService
{
    private readonly NotificationConfig _notificationConfig;
    private readonly IEnumerable<INotificationChannel> _notificationChannels;

    public RegistrationReadinessService(
        NotificationConfig notificationConfig,
        IEnumerable<INotificationChannel> notificationChannels
    )
    {
        _notificationConfig = notificationConfig;
        _notificationChannels = notificationChannels;
    }

    /// <inheritdoc />
    public RegistrationReadiness Evaluate(string? website)
    {
        var websiteValid = Uri.TryCreate(website, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        var emailChannelSelected = string.Equals(
            _notificationConfig.AccountVerificationChannel,
            "email",
            StringComparison.OrdinalIgnoreCase
        );
        var emailChannelAvailable = _notificationChannels.Any(channel => string.Equals(
            channel.Id,
            "email",
            StringComparison.OrdinalIgnoreCase
        ));

        return new(websiteValid, emailChannelSelected, emailChannelAvailable);
    }
}
