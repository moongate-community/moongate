using Moongate.Smtp.Plugin.Data.Exceptions;

namespace Moongate.Smtp.Plugin.Services;

/// <summary>
/// Keeps SMTP credentials off an unencrypted socket. Pure, so the rule can be tested without standing
/// up a server that refuses STARTTLS.
/// </summary>
public static class SmtpCredentialGuard
{
    /// <summary>
    /// Throws <see cref="SmtpInsecureConnectionException" /> when credentials are about to be presented
    /// on a connection that is not encrypted. A connection with no credentials is left alone: a local
    /// relay is a legitimate setup and has no secret to leak.
    /// </summary>
    public static void EnsureCredentialsAreProtected(bool hasCredentials, bool isSecure)
    {
        if (hasCredentials && !isSecure)
        {
            throw new SmtpInsecureConnectionException(
                "Refusing to send SMTP credentials over an unencrypted connection. Security 'Auto' does not guarantee encryption — it falls back to plaintext when the server does not advertise STARTTLS. Set Security to StartTls (port 587) or SslOnConnect (port 465)."
            );
        }
    }
}
