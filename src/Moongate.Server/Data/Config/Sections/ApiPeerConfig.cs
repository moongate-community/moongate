using Moongate.Server.Serialization.Config.Internal;
using Tomlyn.Serialization;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Maps a trusted client certificate to a local process identity and operation permissions.</summary>
public sealed class ApiPeerConfig
{
    public string CertificateSha256 { get; set; } = "";
    public string PeerId { get; set; } = "";
    [TomlConverter(typeof(ApiOperationPermissionsTomlConverter))]
    public ApiOperationPermissionsConfig AllowedOperations { get; set; } = new([]);

    /// <summary>Rejects malformed identities, fingerprints and reserved operation identifiers.</summary>
    public void Validate()
    {
        if (CertificateSha256 is null || CertificateSha256.Length != 64 || !CertificateSha256.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("api.peers.certificate_sha256 must contain exactly 64 hexadecimal characters.");
        }
        if (string.IsNullOrWhiteSpace(PeerId))
        {
            throw new InvalidOperationException("api.peers.peer_id cannot be blank.");
        }
        if (AllowedOperations is null)
        {
            throw new InvalidOperationException("api.peers.allowed_operations cannot be null.");
        }
        AllowedOperations.Validate();
    }
}
