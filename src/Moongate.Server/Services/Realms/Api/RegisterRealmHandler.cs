using System.Net;
using System.Net.Sockets;
using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Realms;
using Moongate.Server.Services.Realms.Internal;

namespace Moongate.Server.Services.Realms.Api;

public sealed class RegisterRealmHandler : IApiHandler<RegisterRealmRequest, RegisterRealmResponse>
{
    private readonly IRealmDirectoryService _directory;

    public RegisterRealmHandler(IRealmDirectoryService directory)
    {
        _directory = directory;
    }

    public ValueTask<RegisterRealmResponse> HandleAsync(
        ApiRequestContext context,
        RegisterRealmRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!StringComparer.Ordinal.Equals(context.Peer.PeerId, request.RealmId))
        {
            return ValueTask.FromResult(Rejected(RealmRegistrationError.IdentityMismatch));
        }

        if (request.InstanceId?.Length != 16 ||
            !IPAddress.TryParse(request.AdvertisedAddress, out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            return ValueTask.FromResult(Rejected(RealmRegistrationError.InvalidDescriptor));
        }

        try
        {
            var registration = new RealmRegistration(
                request.RealmId, request.ServerIndex, request.Name, address, request.AdvertisedPort,
                request.MinimumAccountType
            );
            var lease = _directory.Register(context.Peer.PeerId, registration, new Guid(request.InstanceId));
            return ValueTask.FromResult(new RegisterRealmResponse
            {
                Accepted = true,
                LeaseId = lease.LeaseId.ToByteArray()
            });
        }
        catch (RealmDirectoryException exception)
        {
            return ValueTask.FromResult(Rejected(exception.Error));
        }
        catch (ArgumentException)
        {
            return ValueTask.FromResult(Rejected(RealmRegistrationError.InvalidDescriptor));
        }
        catch (InvalidOperationException)
        {
            return ValueTask.FromResult(Rejected(RealmRegistrationError.InvalidDescriptor));
        }
    }

    private static RegisterRealmResponse Rejected(RealmRegistrationError error)
        => new() { Accepted = false, Error = error };
}
