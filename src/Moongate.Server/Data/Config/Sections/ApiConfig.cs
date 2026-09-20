using System.Net;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Configures the optional internal MessagePack API listener and its mutual TLS policy.</summary>
public sealed class ApiConfig
{
    public bool Enabled { get; set; }
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 2594;
    public string CertificatePath { get; set; } = "";
    public string CertificatePasswordEnvironmentVariable { get; set; } = "MOONGATE_API_CERTIFICATE_PASSWORD";
    public string[] TrustedRootPaths { get; set; } = [];
    public ApiPeerConfig[] Peers { get; set; } = [];

    /// <summary>Validates enabled listeners without reading certificate files or secrets.</summary>
    public void Validate()
    {
        if (!Enabled) { return; }
        if (!IPAddress.TryParse(ListenAddress, out _))
        {
            throw new InvalidOperationException("api.listen_address must be an IP address.");
        }
        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("api.port must be between 1 and 65535.");
        }
        if (string.IsNullOrWhiteSpace(CertificatePath))
        {
            throw new InvalidOperationException("api.certificate_path must name a PFX certificate with a private key.");
        }
        if (CertificatePasswordEnvironmentVariable is null)
        {
            throw new InvalidOperationException("api.certificate_password_environment_variable cannot be null.");
        }
        if (TrustedRootPaths is null || TrustedRootPaths.Length == 0 ||
            TrustedRootPaths.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("api.trusted_root_paths must contain at least one certificate path.");
        }
        if (Peers is null || Peers.Length == 0)
        {
            throw new InvalidOperationException("api.peers must contain at least one allowed peer.");
        }
        var fingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var peer in Peers)
        {
            if (peer is null) { throw new InvalidOperationException("api.peers cannot contain null entries."); }
            peer.Validate();
            if (!fingerprints.Add(peer.CertificateSha256))
            {
                throw new InvalidOperationException("api.peers contains duplicate certificate fingerprints.");
            }
        }
    }
}
