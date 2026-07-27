using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Notifications;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Server.Abstractions.Types;
using Moongate.Smtp.Plugin.Data.Config;
using Moongate.Smtp.Plugin.Data.Exceptions;
using Moongate.Smtp.Plugin.Interfaces;
using Serilog;

namespace Moongate.Smtp.Plugin.Services;

/// <summary>
/// Delivers notifications as email. Building the message is all this class decides; the socket lives
/// behind <see cref="ISmtpTransport" />.
/// </summary>
public sealed class SmtpNotificationChannel : INotificationChannel
{
    private readonly ILogger _logger = Log.ForContext<SmtpNotificationChannel>();
    private readonly ISmtpTransport _transport;
    private readonly MoongateSmtpConfig _config;
    private readonly MoongateConfig _moongateConfig;

    public SmtpNotificationChannel(
        ISmtpTransport transport,
        MoongateSmtpConfig config,
        MoongateConfig moongateConfig
    )
    {
        _transport = transport;
        _config = config;
        _moongateConfig = moongateConfig;
    }

    public string Id => "email";

    public async ValueTask SendAsync(
        NotificationRecipient recipient,
        NotificationContent content,
        CancellationToken cancellationToken = default
    )
    {
        var message = Build(recipient, content);

        try
        {
            await _transport.SendAsync(message, cancellationToken);
        }
        catch (SmtpCommandException exception) when ((int)exception.StatusCode >= 500)
        {
            // Permanent: swallowing it tells the pipeline the delivery is finished, so it does not spend
            // its retries on an answer that will not change.
            _logger.Error(
                exception,
                "SMTP rejected the message for {Address} permanently ({Status}); not retrying",
                recipient.Address,
                exception.StatusCode
            );
        }
        catch (AuthenticationException exception)
        {
            // Also permanent, and worse to retry: repeated bad credentials can trip a provider's limits.
            _logger.Error(exception, "SMTP authentication failed; not retrying");
        }
        catch (SmtpInsecureConnectionException exception)
        {
            // A misconfiguration, so permanent by definition: the connection will be just as unencrypted
            // on the next attempt.
            _logger.Error(exception, "SMTP connection is not encrypted; not retrying");
        }
    }

    private MimeMessage Build(NotificationRecipient recipient, NotificationContent content)
    {
        var fromName = string.IsNullOrWhiteSpace(_config.FromName) ? _moongateConfig.ShardName : _config.FromName;

        var message = new MimeMessage
        {
            Subject = content.Subject ?? string.Empty,
            Body = new TextPart(content.ContentType == NotificationContentType.Html ? TextFormat.Html : TextFormat.Plain)
            {
                Text = content.Body
            }
        };

        message.From.Add(new MailboxAddress(fromName, _config.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient.Address));

        return message;
    }
}
