using System.Net;
using System.Net.Sockets;
using Moongate.Api.Hosting.Internal;
using Moongate.Api.Streams.Internal;
using Moongate.Network.Client;
using Moongate.Network.Data.Config;

namespace Moongate.Api.Tests.Integration.Hosting;

public class ApiAdmissionSetupTests
{
    [Fact]
    public async Task ConfigurationFailureAfterPreparation_ReleasesReservationExactlyOnce()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var releases = 0;
        using var admission = new ApiConnectionAdmission(() => Interlocked.Increment(ref releases));
        var options = new TcpClientOptions
        {
            Pipeline = new()
            {
                PrepareStreamAsync = (stream, _) => ValueTask.FromResult<Stream>(new ApiAdmissionStream(stream, admission)),
                ConfigureClient = _ => throw new InvalidOperationException("Configuration failed.")
            }
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
                        () =>
                            MoongateTcpClient.ConnectConfiguredAsync((IPEndPoint)listener.LocalEndpoint, options)
                    );
        Assert.Equal("Configuration failed.", error.Message);
        Assert.Equal(1, releases);
        Assert.Throws<IOException>(admission.TransferToConnection);
        admission.Dispose();
        Assert.Equal(1, releases);
    }
}
