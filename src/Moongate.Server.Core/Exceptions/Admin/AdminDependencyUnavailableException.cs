namespace Moongate.Server.Core.Exceptions.Admin;

/// <summary>An administration dependency failed; callers must fail closed.</summary>
public sealed class AdminDependencyUnavailableException : Exception
{
    public AdminDependencyUnavailableException() : base("Administration dependency unavailable.")
    {
    }
}
