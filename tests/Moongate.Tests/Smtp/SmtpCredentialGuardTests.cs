using Moongate.Smtp.Plugin.Data.Exceptions;
using Moongate.Smtp.Plugin.Services;

namespace Moongate.Tests.Smtp;

public sealed class SmtpCredentialGuardTests
{
    [Fact]
    public void CredentialsOnAnUnencryptedConnection_Throws()
    {
        // Security: Auto resolves to StartTlsWhenAvailable on any port but the implicit-TLS one, and that
        // proceeds in the clear when the server does not advertise STARTTLS. Authenticating there puts the
        // password on the wire.
        var exception = Assert.Throws<SmtpInsecureConnectionException>(
            () => SmtpCredentialGuard.EnsureCredentialsAreProtected(hasCredentials: true, isSecure: false)
        );

        Assert.Contains("unencrypted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CredentialsOnAnEncryptedConnection_IsAllowed()
        => SmtpCredentialGuard.EnsureCredentialsAreProtected(hasCredentials: true, isSecure: true);

    [Fact]
    public void NoCredentialsOnAnUnencryptedConnection_IsAllowed()
        // A local relay with no authentication is a legitimate setup, and nothing secret crosses the
        // socket. Blocking it would be a broader change than the leak requires.
        => SmtpCredentialGuard.EnsureCredentialsAreProtected(hasCredentials: false, isSecure: false);
}
