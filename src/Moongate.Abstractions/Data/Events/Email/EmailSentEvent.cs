namespace Moongate.Abstractions.Data.Events.Email;

/// <summary>
/// Raised when an email has been sent successfully.
/// </summary>
public sealed class EmailSentEvent
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
