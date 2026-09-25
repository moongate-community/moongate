using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Moongate.Server.Bootstrap.Internal.Setup;

/// <summary>
///     Prepares an offline administration TLS identity while the root initializer owns its lock.
/// </summary>
internal static class AdminCertificateSetup
{
    public static void Configure(string root, IReadOnlyList<string> hosts, TextWriter output)
    {
        var names = NormalizeHosts(hosts);
        var configPath = Path.Combine(root, "config/moongate.toml");
        var original = File.ReadAllText(configPath);
        var updated = AdminApiConfigEditor.EnableGeneratedCertificate(original);
        var certificateDirectory = Path.Combine(root, "certificates");
        var pfxPath = Path.Combine(certificateDirectory, "admin.pfx");
        var publicPath = Path.Combine(certificateDirectory, "admin.crt");
        List<string> created = [];

        try
        {
            if (File.Exists(pfxPath) || File.Exists(publicPath))
            {
                ValidateExisting(pfxPath, publicPath, names);
                output.WriteLine("Preserved existing administration TLS identity.");
            }
            else
            {
                using var key = RSA.Create(3072);
                var request = new CertificateRequest(
                    "CN=Moongate Administration",
                    key,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                );
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true)
                );
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true)
                );
                var san = new SubjectAlternativeNameBuilder();

                foreach (var name in names)
                {
                    if (IPAddress.TryParse(name, out var address))
                    {
                        san.AddIpAddress(address);
                    }
                    else
                    {
                        san.AddDnsName(name);
                    }
                }

                request.CertificateExtensions.Add(san.Build());
                using var certificate = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddDays(365)
                );
                var pfx = certificate.Export(X509ContentType.Pfx, "");

                try
                {
                    Directory.CreateDirectory(certificateDirectory);
                    WriteNew(pfxPath, pfx, UnixFileMode.UserRead | UnixFileMode.UserWrite, created);
                    WriteNew(
                        publicPath,
                        Encoding.UTF8.GetBytes(certificate.ExportCertificatePem()),
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
                        created
                    );
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(pfx);
                }
            }

            if (original != updated)
            {
                ReplaceConfig(configPath, updated);
            }
        }
        catch
        {
            foreach (var path in created)
            {
                File.Delete(path);
            }

            throw;
        }

        output.WriteLine($"Administration TLS configured and enabled: {pfxPath}");
        output.WriteLine($"Trust this public certificate in administration clients: {publicPath}");
    }

    public static IReadOnlyList<string> NormalizeHosts(IReadOnlyList<string> hosts)
    {
        List<string> names = ["localhost", "127.0.0.1", "::1"];

        foreach (var value in hosts)
        {
            var name = value.Trim();

            if (IPAddress.TryParse(name, out var address))
            {
                if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                {
                    throw new ArgumentException(
                        "Certificate hosts must name a reachable DNS name or IP address, not a wildcard bind address.",
                        nameof(hosts)
                    );
                }

                name = address.ToString();
            }
            else if (Uri.CheckHostName(name) != UriHostNameType.Dns || name.Contains('*'))
            {
                throw new ArgumentException(
                    "Certificate hosts must be DNS names or IP addresses without URL schemes, ports or wildcards.",
                    nameof(hosts)
                );
            }
            else
            {
                name = new IdnMapping().GetAscii(name);
            }

            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static void ValidateExisting(string pfxPath, string publicPath, IReadOnlyList<string> names)
    {
        if (!File.Exists(pfxPath) || !File.Exists(publicPath))
        {
            throw new InvalidOperationException(
                "An incomplete administration certificate pair exists. No files were overwritten."
            );
        }

        try
        {
            using var certificate =
                X509CertificateLoader.LoadPkcs12FromFile(pfxPath, "", X509KeyStorageFlags.EphemeralKeySet);
            using var publicCertificate = X509CertificateLoader.LoadCertificateFromFile(publicPath);

            if (!certificate.HasPrivateKey ||
                !SupportsServerAuthentication(certificate) ||
                certificate.Thumbprint != publicCertificate.Thumbprint ||
                certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
                certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow ||
                names.Any(name => !certificate.MatchesHostname(name, false, false)))
            {
                throw new InvalidOperationException(
                    "Existing administration certificates are invalid, expired, mismatched, unsuitable for server TLS or missing requested host names. No files were overwritten."
                );
            }
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "Existing administration certificates cannot be read as a passwordless PFX and public PEM. No files were overwritten."
            );
        }
    }

    private static bool SupportsServerAuthentication(X509Certificate2 certificate)
    {
        return certificate.Extensions
                   .OfType<X509EnhancedKeyUsageExtension>()
                   .All(extension =>
                       extension.EnhancedKeyUsages
                           .Cast<Oid>()
                           .Any(oid => oid.Value is "1.3.6.1.5.5.7.3.1" or "2.5.29.37.0")
                   ) &&
               certificate.Extensions
                   .OfType<X509KeyUsageExtension>()
                   .All(extension =>
                       (extension.KeyUsages & X509KeyUsageFlags.DigitalSignature) != 0
                   );
    }

    private static void WriteNew(string path, byte[] contents, UnixFileMode mode, List<string> created)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = mode;
        }

        using var stream = new FileStream(path, options);
        created.Add(path);
        stream.Write(contents);
        stream.Flush(true);
    }

    private static void ReplaceConfig(string path, string text)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        List<string> created = [];

        try
        {
            var mode = OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(path);
            WriteNew(temporary, Encoding.UTF8.GetBytes(text), mode, created);
            File.Move(temporary, path, true);
        }
        finally
        {
            foreach (var file in created)
            {
                File.Delete(file);
            }
        }
    }
}
