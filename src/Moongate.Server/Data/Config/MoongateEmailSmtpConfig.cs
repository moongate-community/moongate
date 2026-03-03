namespace Moongate.Server.Data.Config;

/// <summary>
/// Represents SMTP transport configuration.
/// </summary>
public class MoongateEmailSmtpConfig
{
    public string Host { get; set; }

    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string Username { get; set; }

    public string Password { get; set; }
}
