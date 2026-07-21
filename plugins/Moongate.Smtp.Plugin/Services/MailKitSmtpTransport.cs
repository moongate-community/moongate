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

        // Checked after connecting, because whether the connection ended up encrypted is only known
        // then: SecureSocketOptions.Auto resolves to StartTlsWhenAvailable on any port but the
        // implicit-TLS one, and that proceeds in the clear when the server offers no STARTTLS.
        var hasCredentials = !string.IsNullOrWhiteSpace(_config.Username);
        SmtpCredentialGuard.EnsureCredentialsAreProtected(hasCredentials, client.IsSecure);

        if (hasCredentials)
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
