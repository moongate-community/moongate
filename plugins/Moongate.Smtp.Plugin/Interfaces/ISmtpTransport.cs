using MimeKit;

namespace Moongate.Smtp.Plugin.Interfaces;

/// <summary>
/// Delivers one message over SMTP. The seam exists so building the message stays pure and testable, and
/// everything that touches a socket lives behind a single method.
/// </summary>
public interface ISmtpTransport
{
    /// <summary>
    /// Connects, authenticates if configured, sends and disconnects. Throws on failure; the caller
    /// decides which failures are worth retrying.
    /// </summary>
    ValueTask SendAsync(MimeMessage message, CancellationToken cancellationToken = default);
}
