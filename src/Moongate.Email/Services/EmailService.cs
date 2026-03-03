using Moongate.Abstractions.Data.Email;
using Moongate.Abstractions.Interfaces.Services.Email;
using Moongate.Abstractions.Services.Base;

namespace Moongate.Email.Services;

/// <summary>
/// Coordinates template rendering and message delivery.
/// </summary>
public sealed class EmailService : BaseMoongateService, IEmailService
{
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IEmailSender _emailSender;

    public EmailService(IEmailTemplateService emailTemplateService, IEmailSender emailSender)
    {
        _emailTemplateService = emailTemplateService;
        _emailSender = emailSender;
    }

    /// <inheritdoc />
    public async Task<string> QueueAsync(
        string toAddress,
        string fromAddress,
        EmailTemplateRenderRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            throw new ArgumentException("Recipient email is required.", nameof(toAddress));
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new ArgumentException("Sender email is required.", nameof(fromAddress));
        }

        var rendered = await _emailTemplateService.RenderAsync(request, cancellationToken);
        var messageId = Guid.NewGuid().ToString("N");

        var message = new EmailMessage
        {
            MessageId = messageId,
            To = toAddress,
            From = fromAddress,
            TemplateId = request.TemplateId,
            Locale = request.Locale,
            Subject = rendered.Subject,
            HtmlBody = rendered.HtmlBody,
            TextBody = rendered.TextBody
        };

        await _emailSender.SendAsync(message, cancellationToken);

        return messageId;
    }
}
