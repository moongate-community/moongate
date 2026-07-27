using DryIoc;
using Moongate.Server.Abstractions.Interfaces.Notifications;

namespace Moongate.Server.Abstractions.Extensions;

/// <summary>
/// Registers notification channels. Each one is collected under <see cref="INotificationChannel" />, the
/// typed list the notification service resolves to decide who can deliver what.
/// </summary>
public static class NotificationChannelRegistrationExtensions
{
    /// <summary>
    /// Records a notification channel as a singleton <see cref="INotificationChannel" /> so notifications
    /// addressed to its <see cref="INotificationChannel.Id" /> reach it. A plugin adds a transport by
    /// calling this and shipping a template directory named after the same id.
    /// </summary>
    public static IContainer RegisterNotificationChannel<TChannel>(this IContainer container)
        where TChannel : class, INotificationChannel
    {
        container.Register<INotificationChannel, TChannel>(Reuse.Singleton);

        return container;
    }
}
