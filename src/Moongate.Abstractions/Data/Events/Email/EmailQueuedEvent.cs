namespace Moongate.Abstractions.Data.Events.Email;

/// <summary>
/// Raised when an email is queued for delivery.
/// </summary>
public sealed class EmailQueuedEvent
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the recipient email address.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the timestamp in unix milliseconds.
    /// </summary>
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
