using Moongate.Smtp.Plugin.Types;

namespace Moongate.Smtp.Plugin.Data.Config;

/// <summary>
/// SMTP settings, from <c>plugins/configs/smtp.yaml</c>. The channel is registered only when
/// <see cref="Host" /> and <see cref="FromAddress" /> are both set.
/// </summary>
public sealed class MoongateSmtpConfig
{
    /// <summary>SMTP host. Empty leaves the email channel unregistered.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP port. Default 587, the submission port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>How the connection is secured.</summary>
    public SmtpSecurityType Security { get; set; } = SmtpSecurityType.Auto;

    /// <summary>Username. Empty means no authentication, as a local relay usually wants.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Password. Overridden by the MOONGATE_SMTP_PASSWORD environment variable when that is set.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Sender address. Empty leaves the email channel unregistered.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Sender display name. Falls back to the shard name when empty.</summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>Connect and send timeout, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
