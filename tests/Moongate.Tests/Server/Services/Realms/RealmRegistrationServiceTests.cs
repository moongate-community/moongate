using System.Net;
using System.Security.Authentication;
using Moongate.Api.Interfaces.Connections;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Realms;
using Moongate.Server.Core.Types.Realms;
using Moongate.Tests.TestSupport.Realms;

namespace Moongate.Tests.Server.Services.Realms;

public sealed class RealmRegistrationServiceTests
{
    [Fact]
    public async Task Renew_ExpiredLease_RegistersAgainOnSameConnection()
    {
        var connection = new StubStaleRealmConnection(RealmRegistrationError.ExpiredLease);
        await using var client = new StubRealmApiClient(_ => Task.FromResult<IApiConnection>(connection));
        var config = Config();
        config.HeartbeatIntervalSeconds = 1;
        config.LeaseDurationSeconds = 3;
        await using var service = new RealmRegistrationService(client, config, TimeProvider.System);
        await service.StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (connection.Registrations < 2)
        {
            await Task.Delay(20, timeout.Token);
        }

        await service.StopAsync();
    }

    [Fact]
    public async Task Renew_StaleLease_DoesNotSupersedeReplacementAgain()
    {
        var connection = new StubStaleRealmConnection();
        await using var client = new StubRealmApiClient(_ => Task.FromResult<IApiConnection>(connection));
        var config = Config();
        config.HeartbeatIntervalSeconds = 1;
        config.LeaseDurationSeconds = 3;
        await using var service = new RealmRegistrationService(client, config, TimeProvider.System);
        await service.StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (connection.Renewals == 0)
        {
            await Task.Delay(20, timeout.Token);
        }

        await Task.Delay(100);
        Assert.Equal(1, connection.Registrations);
        await service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_PermanentIdentityRejection_DoesNotRetry()
    {
        await using var client = new StubRealmApiClient(_ =>
            Task.FromException<IApiConnection>(new AuthenticationException("identity mismatch")));
        await using var service = new RealmRegistrationService(client, Config(), TimeProvider.System);

        await service.StartAsync();
        await WaitForAttemptAsync(client);
        await Task.Delay(TimeSpan.FromMilliseconds(150));

        Assert.Equal(1, client.Attempts);
        await service.StopAsync();
    }

    [Fact]
    public async Task StopAsync_CancelsInFlightConnection()
    {
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client = new StubRealmApiClient(async token =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unexpected completion.");
            }
            catch (OperationCanceledException)
            {
                canceled.TrySetResult();
                throw;
            }
        });
        await using var service = new RealmRegistrationService(client, Config(), TimeProvider.System);
        await service.StartAsync();
        await WaitForAttemptAsync(client);

        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(canceled.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StartAsync_TransientOutage_UsesBoundedRetryDelay()
    {
        await using var client = new StubRealmApiClient(_ =>
            Task.FromException<IApiConnection>(new IOException("login is down")));
        await using var service = new RealmRegistrationService(client, Config(), TimeProvider.System);
        await service.StartAsync();
        await WaitForAttemptAsync(client);
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.Equal(1, client.Attempts);
        await service.StopAsync();
    }

    private static RealmDirectoryConfig Config()
        => new()
        {
            RealmId = "realm-a", Name = "Realm A", ServerIndex = 1,
            AdvertisedAddress = IPAddress.Loopback.ToString(), AdvertisedPort = 2593,
            LoginApiHost = "localhost", LoginApiPort = 2594, ExpectedLoginPeerId = "login"
        };

    private static async Task WaitForAttemptAsync(StubRealmApiClient client)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (client.Attempts == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }
}
