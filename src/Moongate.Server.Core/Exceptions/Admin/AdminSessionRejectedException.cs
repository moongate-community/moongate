namespace Moongate.Server.Core.Exceptions.Admin;

/// <summary>A session could not be issued for the supplied generation or digest.</summary>
public sealed class AdminSessionRejectedException : Exception
{
    public AdminSessionRejectedException() : base("Administration session issuance rejected.") { }
}
