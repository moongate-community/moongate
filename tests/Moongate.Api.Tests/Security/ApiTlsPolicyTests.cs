using Moongate.Api.Data.Security;
using Moongate.Api.Security.Internal;
using Moongate.Api.Tests.TestSupport.Security;

namespace Moongate.Api.Tests.Security;

public class ApiTlsPolicyTests
{
    [Fact]
    public void CanInvoke_SnapshotsPermissionsAndDeniesUnknownOperations()
    {
        ushort[] permissions = [100];
        var peer = new ApiPeerIdentity("realm-a", permissions);
        permissions[0] = 101;
        Assert.True(peer.CanInvoke(100));
        Assert.False(peer.CanInvoke(101));
    }

    [Fact]
    public void Constructor_RequiresPrivateKeyAndTrustRoots()
    {
        using var ca = new TestCertificateAuthority();
        using var certificate = ca.Issue();
        using var publicOnly = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certificate.RawData);
        Assert.Throws<ArgumentException>(() => new ApiTlsPolicy(ca.Options(publicOnly, certificate, "peer")));
        Assert.Throws<ArgumentException>(() => new ApiTlsPolicy(ca.Options(certificate, certificate, "peer") with { TrustedRoots = [] }));
    }
}
