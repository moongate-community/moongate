using Moongate.Api.Data.Security;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Interfaces.Contracts;
using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Types.Realms;

namespace Moongate.Tests.TestSupport.Realms;

internal sealed class StubStaleRealmConnection : IApiConnection
{
    private readonly RealmRegistrationError _renewalError;
    private int _registrations;
    private int _renewals;

    public int Registrations => Volatile.Read(ref _registrations);
    public int Renewals => Volatile.Read(ref _renewals);
    public long ConnectionId => 1;
    public ApiPeerIdentity Peer { get; } = new("login", []);
    public Task Completion => Task.CompletedTask;

    public StubStaleRealmConnection(RealmRegistrationError renewalError = RealmRegistrationError.StaleLease)
    {
        _renewalError = renewalError;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) where TRequest : IApiRequest<TResponse>
    {
        object response = request switch
        {
            RegisterRealmRequest => Register(),
            RenewRealmRequest => Renew(),
            UnregisterRealmRequest => new UnregisterRealmResponse { Accepted = false },
            _ => throw new NotSupportedException()
        };
        return Task.FromResult((TResponse)response);
    }

    private RegisterRealmResponse Register()
    {
        Interlocked.Increment(ref _registrations);
        return new() { Accepted = true, LeaseId = Guid.NewGuid().ToByteArray() };
    }

    private RenewRealmResponse Renew()
    {
        Interlocked.Increment(ref _renewals);
        return new() { Accepted = false, Error = _renewalError };
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
