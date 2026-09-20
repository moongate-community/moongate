using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Security;
using Moongate.Api.Registry;
using Moongate.Api.Server;
using Moongate.Core.Directories;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Server.Services.Api.Internal;

internal static class ApiServerFactory
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private const string AnyExtendedKeyUsageOid = "2.5.29.37.0";

    public static ApiServer Create(ApiConfig config, DirectoriesConfig directories, ApiRegistry registry, TimeProvider clock)
    {
        var passwordVariable = config.CertificatePasswordEnvironmentVariable;
        var password = passwordVariable.Length == 0 ? null : Environment.GetEnvironmentVariable(passwordVariable);
        if (passwordVariable.Length > 0 && password is null)
        {
            throw new InvalidOperationException($"The API certificate password environment variable '{passwordVariable}' is not set.");
        }

        var configDirectory = directories["config"];
        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            Path.GetFullPath(config.CertificatePath, configDirectory), password, X509KeyStorageFlags.EphemeralKeySet);
        ValidateServerCertificate(certificate, clock);
        var roots = new List<X509Certificate2>();
        try
        {
            foreach (var path in config.TrustedRootPaths)
            {
                roots.Add(X509CertificateLoader.LoadCertificateFromFile(Path.GetFullPath(path, configDirectory)));
            }
            var peers = config.Peers.ToDictionary(
                peer => peer.CertificateSha256,
                peer => new ApiPeerIdentity(peer.PeerId, peer.AllowedOperations),
                StringComparer.OrdinalIgnoreCase);
            return new ApiServer(new IPEndPoint(IPAddress.Parse(config.ListenAddress), config.Port), registry,
                new ApiOptions(), new ApiTlsOptions
                {
                    Certificate = certificate,
                    TrustedRoots = roots,
                    PeersByCertificateSha256 = peers
                }, clock);
        }
        finally
        {
            // ApiServer snapshots certificate handles before these caller-owned handles are released.
            foreach (var root in roots) { root.Dispose(); }
        }
    }

    private static void ValidateServerCertificate(X509Certificate2 certificate, TimeProvider clock)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("The API server certificate must include its private key.");
        }
        var now = clock.GetUtcNow().UtcDateTime;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            throw new InvalidOperationException("The API server certificate is not valid at the current time.");
        }
        // No EKU extension means unrestricted usage. Client trust roots need not issue the server's leaf.
        foreach (var usage in certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>())
        {
            if (!usage.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value is ServerAuthenticationOid or AnyExtendedKeyUsageOid))
            {
                throw new InvalidOperationException("The API server certificate must allow TLS server authentication.");
            }
        }
    }

}
