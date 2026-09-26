namespace Moongate.Server.Core.Exceptions.Admin;

/// <summary>
///     The account has reached its bounded session capacity.
/// </summary>
public sealed class AdminSessionLimitException : Exception
{
    public AdminSessionLimitException() : base("Administration session limit reached.")
    {
    }
}
