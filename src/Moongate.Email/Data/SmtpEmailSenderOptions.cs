namespace Moongate.Email.Data;

/// <summary>
/// Represents SMTP transport settings used by <see cref="Services.SmtpEmailSender" />.
/// </summary>
public sealed class SmtpEmailSenderOptions
{
    /// <summary>
    /// Gets or sets SMTP host name.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets SMTP port.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Gets or sets whether SSL/TLS is enabled.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Gets or sets SMTP user name for authenticated relay.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets SMTP password for authenticated relay.
    /// </summary>
    public string Password { get; set; }
}
