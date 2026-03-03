using Moongate.Abstractions.Data.Email;
using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Abstractions.Interfaces.Services.Email;

/// <summary>
/// Defines template rendering operations for email payloads.
/// </summary>
public interface IEmailTemplateService : IMoongateService
{
    /// <summary>
    /// Renders subject and bodies for the given template request.
    /// </summary>
    /// <param name="request">Template render request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered email payload.</returns>
    Task<EmailTemplateRenderResult> RenderAsync(
        EmailTemplateRenderRequest request,
        CancellationToken cancellationToken = default
    );
}
