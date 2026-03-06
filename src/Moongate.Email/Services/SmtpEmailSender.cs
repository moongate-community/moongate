using System.Net;
using System.Net.Mail;
using Moongate.Abstractions.Data.Email;
using Moongate.Abstractions.Interfaces.Services.Email;
using Moongate.Email.Data;

namespace Moongate.Email.Services;

/// <summary>
/// Sends rendered email messages through SMTP.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailSenderOptions _options;

    public SmtpEmailSender(SmtpEmailSenderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("SMTP host is not configured.");
        }

        if (string.IsNullOrWhiteSpace(message.To))
        {
            throw new InvalidOperationException("Recipient email address is required.");
        }

        if (string.IsNullOrWhiteSpace(message.From))
        {
            throw new InvalidOperationException("Sender email address is required.");
        }

        using var mail = new MailMessage
        {
            From = new(message.From),
            Subject = message.Subject,
            Body = message.TextBody ?? string.Empty,
            IsBodyHtml = false
        };

        mail.To.Add(new MailAddress(message.To));

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mail.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    message.HtmlBody,
                    null,
                    "text/html"
                )
            );
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }
        else
        {
            client.UseDefaultCredentials = true;
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}
