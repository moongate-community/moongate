using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Moongate.Smtp.Plugin.Data.Config;
using Moongate.Smtp.Plugin.Interfaces;
using Moongate.Smtp.Plugin.Types;

namespace Moongate.Smtp.Plugin.Services;

/// <summary>
/// The only code in Moongate that speaks MailKit. One connection per message: pooling would buy nothing
/// at these volumes and would put shared state on the worker threads that deliver notifications.
/// </summary>
public sealed class MailKitSmtpTransport : ISmtpTransport
{
    private readonly MoongateSmtpConfig _config;

    public MailKitSmtpTransport(MoongateSmtpConfig config)
    {
        _config = config;
    }

    public async ValueTask SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient
        {
            Timeout = _config.TimeoutSeconds * 1000
        };

        await client.ConnectAsync(_config.Host, _config.Port, ToSocketOptions(_config.Security), cancellationToken);

        if (!string.IsNullOrWhiteSpace(_config.Username))
        {
            await client.AuthenticateAsync(_config.Username, _config.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurityType security)
        => security switch
        {
            SmtpSecurityType.None         => SecureSocketOptions.None,
            SmtpSecurityType.StartTls     => SecureSocketOptions.StartTls,
            SmtpSecurityType.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _                             => SecureSocketOptions.Auto
        };
}
