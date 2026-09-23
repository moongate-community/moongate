using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;
using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Realms;

namespace Moongate.Server.Services.Realms.Api;

public sealed class UnregisterRealmHandler : IApiHandler<UnregisterRealmRequest, UnregisterRealmResponse>
{
    private readonly IRealmDirectoryService _directory;

    public UnregisterRealmHandler(IRealmDirectoryService directory)
    {
        _directory = directory;
    }

    public ValueTask<UnregisterRealmResponse> HandleAsync(
        ApiRequestContext context,
        UnregisterRealmRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!StringComparer.Ordinal.Equals(context.Peer.PeerId, request.RealmId))
        {
            return ValueTask.FromResult(new UnregisterRealmResponse { Error = RealmRegistrationError.IdentityMismatch });
        }

        if (request.LeaseId?.Length != 16)
        {
            return ValueTask.FromResult(new UnregisterRealmResponse { Error = RealmRegistrationError.InvalidDescriptor });
        }

        var accepted = _directory.Unregister(context.Peer.PeerId, request.RealmId, new Guid(request.LeaseId));
        return ValueTask.FromResult(new UnregisterRealmResponse
        {
            Accepted = accepted,
            Error = accepted ? RealmRegistrationError.None : RealmRegistrationError.StaleLease
        });
    }
}
