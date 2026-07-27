namespace Moongate.Server.Abstractions.Data.Config;

/// <summary>Notification delivery settings, loaded from the <c>notifications</c> section of moongate.yaml.</summary>
public sealed class NotificationConfig
{
    /// <summary>
    /// Which channel account verification is delivered on. Defaults to <c>log</c>, the only channel a
    /// bare shard has; point it at <c>email</c> once an SMTP transport is installed and configured.
    /// </summary>
    public string AccountVerificationChannel { get; set; } = "log";

    public int MaxAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 5;
}
