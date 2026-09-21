using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Security;

namespace Moongate.Tests.TestSupport.Api;

internal sealed class ApiTestCertificateAuthority : IDisposable
{
    public X509Certificate2 Root { get; }

    public ApiTestCertificateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Moongate test CA",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        Root = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30));
    }

    public void Dispose()
        => Root.Dispose();

    public X509Certificate2 Issue(
        string name = "localhost",
        bool expired = false,
        bool clientOnly = false,
        bool notYetValid = false
    )
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var usages = new OidCollection { new("1.3.6.1.5.5.7.3.2") };

        if (!clientOnly)
        {
            usages.Add(new("1.3.6.1.5.5.7.3.1"));
        }

        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(name);
        request.CertificateExtensions.Add(san.Build());
        using var certificate = request.Create(
            Root,
            DateTimeOffset.UtcNow.AddDays(notYetValid ? 1 : -2),
            DateTimeOffset.UtcNow.AddDays(expired ? -1 : 2),
            RandomNumberGenerator.GetBytes(16)
        );

        return certificate.CopyWithPrivateKey(key);
    }

    public ApiTlsOptions Options(X509Certificate2 local, X509Certificate2 remote, string remoteId)
        => Options(Root, local, remote, remoteId);

    public static ApiTlsOptions Options(
        X509Certificate2 root,
        X509Certificate2 local,
        X509Certificate2 remote,
        string remoteId
    )
        => new()
        {
            Certificate = local,
            TrustedRoots = new[] { root },
            PeersByCertificateSha256 = new Dictionary<string, ApiPeerIdentity>
            {
                [remote.GetCertHashString(HashAlgorithmName.SHA256)] = new(remoteId, new ushort[] { 100 })
            }
        };
}
