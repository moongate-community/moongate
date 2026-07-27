using MailKit.Security;
using Moongate.Smtp.Plugin.Types;

namespace Moongate.Smtp.Plugin.Extensions;

/// <summary>Maps the plugin's security setting onto the socket options MailKit expects.</summary>
public static class SmtpSecurityTypeExtensions
{
    /// <summary>
    /// Returns the MailKit socket options for <paramref name="security" />. An unrecognised value maps to
    /// <see cref="SecureSocketOptions.Auto" />, which is also what the configuration defaults to.
    /// </summary>
    /// <remarks>
    /// <see cref="SecureSocketOptions.Auto" /> does not guarantee encryption: it picks implicit TLS only
    /// on the implicit-TLS port and otherwise upgrades with STARTTLS *when the server offers it*.
    /// SmtpCredentialGuard is what stops credentials going out on a connection that stayed in the clear.
    /// </remarks>
    public static SecureSocketOptions ToSocketOptions(this SmtpSecurityType security)
        => security switch
        {
            SmtpSecurityType.None         => SecureSocketOptions.None,
            SmtpSecurityType.StartTls     => SecureSocketOptions.StartTls,
            SmtpSecurityType.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _                             => SecureSocketOptions.Auto
        };
}
