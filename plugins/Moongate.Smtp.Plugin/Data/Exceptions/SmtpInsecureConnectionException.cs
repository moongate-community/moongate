namespace Moongate.Smtp.Plugin.Data.Exceptions;

/// <summary>
/// Raised when credentials would be sent over a connection that never became encrypted. Its own type
/// rather than a general-purpose exception, so the channel can classify it as permanent without
/// swallowing unrelated failures.
/// </summary>
public sealed class SmtpInsecureConnectionException : Exception
{
    public SmtpInsecureConnectionException(string message) : base(message)
    {
    }
}
