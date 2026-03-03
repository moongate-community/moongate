using Moongate.Abstractions.Data.Email;
using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Abstractions.Interfaces.Services.Email;

/// <summary>
/// Defines high-level email orchestration operations.
/// </summary>
public interface IEmailService : IMoongateService
{
    /// <summary>
    /// Queues and sends an email from a template.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="from">Sender email address.</param>
    /// <param name="request">Template render request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created message identifier.</returns>
    Task<string> QueueAsync(
        string toAddress,
        string fromAddress,
        EmailTemplateRenderRequest request,
        CancellationToken cancellationToken = default
    );
}
