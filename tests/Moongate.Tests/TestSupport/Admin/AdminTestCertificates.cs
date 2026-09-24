using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class AdminTestCertificates : IDisposable
{
    private readonly TemporaryPersistenceDirectory _directory = new();
    public X509Certificate2 Root { get; }
    public string PfxPath { get; }
    public string RootPemPath { get; }

    public AdminTestCertificates()
    {
        using var caKey = RSA.Create(2048);
        var caRequest = new CertificateRequest("CN=Moongate Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        Root = X509CertificateLoader.LoadCertificate(ca.Export(X509ContentType.Cert));
        using var serverKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
        using var withKey = certificate.CopyWithPrivateKey(serverKey);
        PfxPath = Path.Combine(_directory.Path, "server.pfx");
        RootPemPath = Path.Combine(_directory.Path, "ca.pem");
        File.WriteAllBytes(PfxPath, withKey.Export(X509ContentType.Pfx));
        File.WriteAllText(RootPemPath, Root.ExportCertificatePem());
    }

    public SocketsHttpHandler CreateHandler()
    {
        var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
        policy.CustomTrustStore.Add(Root);
        return new() { SslOptions = new() { CertificateChainPolicy = policy } };
    }

    public void Dispose() { Root.Dispose(); _directory.Dispose(); }
}
