namespace Moongate.Server.Abstractions.Data.Config;

/// <summary>Notification delivery settings, loaded from the <c>notifications</c> section of moongate.yaml.</summary>
public sealed class NotificationConfig
{
    public int MaxAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 5;
}
