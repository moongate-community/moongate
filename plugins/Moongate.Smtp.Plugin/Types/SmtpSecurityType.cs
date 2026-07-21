namespace Moongate.Smtp.Plugin.Types;

/// <summary>How the SMTP connection is secured. Mapped onto MailKit's socket options by the transport.</summary>
public enum SmtpSecurityType
{
    /// <summary>Let the transport choose from the port: implicit TLS on 465, STARTTLS elsewhere when offered.</summary>
    Auto,

    /// <summary>No encryption. Only sensible for a relay on localhost.</summary>
    None,

    /// <summary>Connect in the clear and upgrade with STARTTLS, failing if the server does not offer it.</summary>
    StartTls,

    /// <summary>Negotiate TLS immediately on connect, as port 465 expects.</summary>
    SslOnConnect
}
