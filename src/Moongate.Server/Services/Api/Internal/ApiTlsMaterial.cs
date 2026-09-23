using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Security;
using Moongate.Core.Directories;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Server.Services.Api.Internal;

internal sealed class ApiTlsMaterial : IDisposable
{
    private readonly X509Certificate2 _certificate;
    private readonly List<X509Certificate2> _roots;

    public ApiTlsOptions Options { get; }

    private ApiTlsMaterial(X509Certificate2 certificate, List<X509Certificate2> roots,
        IReadOnlyDictionary<string, ApiPeerIdentity> peers)
    {
        _certificate = certificate;
        _roots = roots;
        Options = new() { Certificate = certificate, TrustedRoots = roots, PeersByCertificateSha256 = peers };
    }

    public static ApiTlsMaterial Load(ApiConfig config, DirectoriesConfig directories, TimeProvider clock,
        bool forClient = false)
    {
        if (config.TrustedRootPaths is null || config.TrustedRootPaths.Length == 0 ||
            config.TrustedRootPaths.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("api.trusted_root_paths must contain at least one certificate path.");
        }

        if (config.Peers is null || config.Peers.Length == 0)
        {
            throw new InvalidOperationException("api.peers must contain at least one allowed peer.");
        }

        foreach (var peer in config.Peers)
        {
            peer.Validate();
        }

        var certificate = new ApiCertificateStore().Load(config, directories, clock, forClient);
        var roots = new List<X509Certificate2>();

        try
        {
            foreach (var path in config.TrustedRootPaths)
            {
                roots.Add(X509CertificateLoader.LoadCertificateFromFile(
                    Path.GetFullPath(path, directories["config"])));
            }

            var peers = config.Peers.ToDictionary(
                peer => peer.CertificateSha256,
                peer => new ApiPeerIdentity(peer.PeerId, peer.AllowedOperations.OperationIds,
                    peer.AllowedOperations.AllowsAll),
                StringComparer.OrdinalIgnoreCase);
            return new(certificate, roots, peers);
        }
        catch
        {
            certificate.Dispose();
            foreach (var root in roots)
            {
                root.Dispose();
            }

            throw;
        }
    }

    public void Dispose()
    {
        _certificate.Dispose();
        foreach (var root in _roots)
        {
            root.Dispose();
        }
    }
}
