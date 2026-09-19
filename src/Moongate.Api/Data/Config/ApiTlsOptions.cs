using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Data.Security;

namespace Moongate.Api.Data.Config;

/// <summary>Caller-owned certificates and local peer policy, copied when the endpoint is constructed.</summary>
public sealed record ApiTlsOptions
{
    /// <summary>Gets the local leaf certificate, including its private key.</summary>
    public required X509Certificate2 Certificate { get; init; }
    /// <summary>Gets the private CA roots trusted exclusively by this endpoint.</summary>
    public required IReadOnlyList<X509Certificate2> TrustedRoots { get; init; }
    /// <summary>Gets allowed leaf SHA-256 fingerprints (hex) mapped to immutable identities.</summary>
    public required IReadOnlyDictionary<string, ApiPeerIdentity> PeersByCertificateSha256 { get; init; }
}
