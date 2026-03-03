using Moongate.Abstractions.Data.Email;
using Moongate.Abstractions.Interfaces.Services.Email;

namespace Moongate.Email.Services;

/// <summary>
/// Default sender implementation that intentionally performs no external delivery.
/// </summary>
public sealed class NoOpEmailSender : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _ = cancellationToken;

        return Task.CompletedTask;
    }
}
