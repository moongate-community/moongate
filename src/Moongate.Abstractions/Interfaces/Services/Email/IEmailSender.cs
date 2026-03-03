using Moongate.Abstractions.Data.Email;

namespace Moongate.Abstractions.Interfaces.Services.Email;

/// <summary>
/// Defines low-level email delivery operations.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends a rendered message to the configured provider.
    /// </summary>
    /// <param name="message">Rendered email message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
