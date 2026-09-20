using System.Net;
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
    public static ApiServer Create(ApiConfig config, DirectoriesConfig directories, ApiRegistry registry, TimeProvider clock)
    {
        using var certificate = new ApiCertificateStore().Load(config, directories, clock);
        var configDirectory = directories["config"];
        var roots = new List<X509Certificate2>();
        try
        {
            foreach (var path in config.TrustedRootPaths)
            {
                roots.Add(X509CertificateLoader.LoadCertificateFromFile(Path.GetFullPath(path, configDirectory)));
            }
            var peers = config.Peers.ToDictionary(
                peer => peer.CertificateSha256,
                peer => new ApiPeerIdentity(peer.PeerId, peer.AllowedOperations.OperationIds, peer.AllowedOperations.AllowsAll),
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
}
