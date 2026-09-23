using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;
using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Realms;

namespace Moongate.Server.Services.Realms.Api;

public sealed class RenewRealmHandler : IApiHandler<RenewRealmRequest, RenewRealmResponse>
{
    private readonly IRealmDirectoryService _directory;

    public RenewRealmHandler(IRealmDirectoryService directory)
    {
        _directory = directory;
    }

    public ValueTask<RenewRealmResponse> HandleAsync(
        ApiRequestContext context,
        RenewRealmRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!StringComparer.Ordinal.Equals(context.Peer.PeerId, request.RealmId))
        {
            return ValueTask.FromResult(new RenewRealmResponse { Error = RealmRegistrationError.IdentityMismatch });
        }

        if (request.LeaseId?.Length != 16)
        {
            return ValueTask.FromResult(new RenewRealmResponse { Error = RealmRegistrationError.InvalidDescriptor });
        }

        var error = _directory.Renew(context.Peer.PeerId, request.RealmId, new Guid(request.LeaseId));
        return ValueTask.FromResult(new RenewRealmResponse
        {
            Accepted = error == RealmRegistrationError.None,
            Error = error
        });
    }
}
