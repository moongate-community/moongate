using Moongate.Server.Core.Types.Realms;

namespace Moongate.Server.Services.Realms.Internal;

internal sealed class RealmDirectoryException : InvalidOperationException
{
    public RealmRegistrationError Error { get; }

    public RealmDirectoryException(RealmRegistrationError error, string message) : base(message)
    {
        Error = error;
    }
}
