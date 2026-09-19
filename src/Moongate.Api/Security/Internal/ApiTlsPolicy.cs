using System.Collections.Frozen;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Security;

namespace Moongate.Api.Security.Internal;

internal sealed class ApiTlsPolicy : IDisposable
{
    private readonly X509Certificate2 _certificate;
    private readonly X509Certificate2[] _roots;
    private readonly FrozenDictionary<string, ApiPeerIdentity> _peers;

    public ApiTlsPolicy(ApiTlsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Certificate);
        ArgumentNullException.ThrowIfNull(options.TrustedRoots);
        ArgumentNullException.ThrowIfNull(options.PeersByCertificateSha256);

        if (!options.Certificate.HasPrivateKey)
        {
            throw new ArgumentException("The local TLS certificate must have a private key.", nameof(options));
        }

        if (options.TrustedRoots.Count == 0)
        {
            throw new ArgumentException("At least one private trust root is required.", nameof(options));
        }
        var peers = new Dictionary<string, ApiPeerIdentity>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fingerprint, peer) in options.PeersByCertificateSha256)
        {
            if (fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit) || peer is null)
            {
                throw new ArgumentException("Peer entries require a SHA-256 hex fingerprint and identity.", nameof(options));
            }
            peers.Add(fingerprint, peer);
        }
        _peers = peers.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        var copies = new List<X509Certificate2>();

        try
        {
            _certificate = new(options.Certificate);
            copies.Add(_certificate);

            foreach (var root in options.TrustedRoots) { copies.Add(new(root)); }
            _roots = copies.Skip(1).ToArray();
        }
        catch
        {
            foreach (var copy in copies) { copy.Dispose(); }

            throw;
        }
    }

    public async ValueTask<Stream> PrepareClientAsync(
        Stream stream,
        string targetHost,
        string expectedPeerId,
        Action<ApiPeerIdentity> authenticated,
        CancellationToken token
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPeerId);
        var ssl = new SslStream(stream, false);
        ApiPeerIdentity? identity = null;

        try
        {
            await ssl.AuthenticateAsClientAsync(
                         new()
                         {
                             TargetHost = targetHost,
                             ClientCertificates = new() { _certificate },
                             CertificateChainPolicy = CreateChainPolicy("1.3.6.1.5.5.7.3.1"),
                             EnabledSslProtocols = SslProtocols.None,
                             AllowRenegotiation = false,
                             AllowTlsResume = false,
                             RemoteCertificateValidationCallback = (_, certificate, _, errors)
                                                                       => TryAuthenticate(
                                                                           certificate,
                                                                           errors,
                                                                           expectedPeerId,
                                                                           out identity
                                                                       )
                         },
                         token
                     )
                     .ConfigureAwait(false);

            if (identity is null)
            {
                throw new AuthenticationException("The remote certificate did not identify the expected peer.");
            }
            token.ThrowIfCancellationRequested();
            authenticated(identity);

            return ssl;
        }
        catch
        {
            await ssl.DisposeAsync().ConfigureAwait(false);

            throw;
        }
    }

    public async ValueTask<Stream> PrepareServerAsync(
        Stream stream,
        Action<ApiPeerIdentity> authenticated,
        CancellationToken token
    )
    {
        var ssl = new SslStream(stream, false);
        ApiPeerIdentity? identity = null;

        try
        {
            await ssl.AuthenticateAsServerAsync(
                         new()
                         {
                             ServerCertificate = _certificate,
                             ClientCertificateRequired = true,
                             CertificateChainPolicy = CreateChainPolicy("1.3.6.1.5.5.7.3.2"),
                             EnabledSslProtocols = SslProtocols.None,
                             AllowRenegotiation = false,
                             AllowTlsResume = false,
                             RemoteCertificateValidationCallback = (_, certificate, _, errors)
                                                                       => TryAuthenticate(
                                                                           certificate,
                                                                           errors,
                                                                           null,
                                                                           out identity
                                                                       )
                         },
                         token
                     )
                     .ConfigureAwait(false);

            if (identity is null)
            {
                throw new AuthenticationException("The remote certificate did not identify an allowed peer.");
            }
            token.ThrowIfCancellationRequested();
            authenticated(identity);

            return ssl;
        }
        catch
        {
            await ssl.DisposeAsync().ConfigureAwait(false);

            throw;
        }
    }

    private X509ChainPolicy CreateChainPolicy(string applicationOid)
    {
        var policy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            VerificationFlags = X509VerificationFlags.NoFlag,
            DisableCertificateDownloads = true,
            RevocationMode = X509RevocationMode.NoCheck
        };
        policy.ApplicationPolicy.Add(new(applicationOid));
        policy.CustomTrustStore.AddRange(_roots);

        return policy;
    }

    private bool TryAuthenticate(
        X509Certificate? certificate,
        SslPolicyErrors errors,
        string? expectedPeerId,
        out ApiPeerIdentity? identity
    )
    {
        identity = null;

        if (errors != SslPolicyErrors.None || certificate is null) { return false; }

        if (!_peers.TryGetValue(certificate.GetCertHashString(HashAlgorithmName.SHA256), out var peer)) { return false; }

        if (expectedPeerId is not null && !string.Equals(expectedPeerId, peer.PeerId, StringComparison.Ordinal))
        {
            return false;
        }
        identity = peer;

        return true;
    }

    public void Dispose()
    {
        _certificate.Dispose();

        foreach (var root in _roots) { root.Dispose(); }
    }
}
