using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Core.Directories;
using Moongate.Server.Data.Config.Sections;
using Serilog;

namespace Moongate.Server.Services.Api.Internal;

internal sealed class ApiCertificateStore
{
    private const int KeySize = 2048;
    private const int ValidityDays = 365;
    private const int ClockSkewMinutes = 5;
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private const string AnyExtendedKeyUsageOid = "2.5.29.37.0";

    private readonly ILogger _logger = Log.ForContext<ApiCertificateStore>();

    public X509Certificate2 Load(ApiConfig config, DirectoriesConfig directories, TimeProvider clock)
    {
        config.ValidateCertificate();
        var passwordVariable = config.CertificatePasswordEnvironmentVariable;
        var password = passwordVariable.Length == 0 ? null : Environment.GetEnvironmentVariable(passwordVariable);
        if (passwordVariable.Length > 0 && password is null)
        {
            throw new InvalidOperationException($"The API certificate password environment variable '{passwordVariable}' is not set.");
        }

        var path = Path.GetFullPath(config.CertificatePath, directories["config"]);
        var generated = false;
        if (!File.Exists(path) && config.AutoGenerateCertificate)
        {
            generated = Generate(config, path, password, clock);
        }
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
        try
        {
            ValidateServerCertificate(certificate, clock);
            if (config.AutoGenerateCertificate)
            {
                // Appending also supports a configured PFX path that already ends in .pem.
                var publicPath = path + ".pem";
                var pem = certificate.ExportCertificatePem();
                if (!File.Exists(publicPath) || File.ReadAllText(publicPath) != pem)
                {
                    Publish(publicPath, System.Text.Encoding.ASCII.GetBytes(pem), overwrite: true);
                }
                _logger.Information("API certificate public copy available at {PublicCertificatePath}", publicPath);
            }
            _logger.Information("API certificate loaded from {CertificatePath}; generated: {Generated}; SHA-256: {CertificateSha256}; expires: {ExpiresAt}",
                path, generated, certificate.GetCertHashString(HashAlgorithmName.SHA256), certificate.NotAfter.ToUniversalTime());
            return certificate;
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    private static bool Generate(ApiConfig config, string path, string? password, TimeProvider clock)
    {
        var directory = Path.GetDirectoryName(path)!;
        if (OperatingSystem.IsWindows()) { Directory.CreateDirectory(directory); }
        else { Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }

        using var key = RSA.Create(KeySize);
        var request = new CertificateRequest("CN=Moongate API", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(ServerAuthenticationOid), new(ClientAuthenticationOid) }, true));
        var names = new SubjectAlternativeNameBuilder();
        foreach (var name in config.CertificateDnsNames) { names.AddDnsName(name); }
        foreach (var address in config.CertificateIpAddresses) { names.AddIpAddress(IPAddress.Parse(address)); }
        request.CertificateExtensions.Add(names.Build());
        var now = clock.GetUtcNow();
        using var certificate = request.CreateSelfSigned(now.AddMinutes(-ClockSkewMinutes), now.AddDays(ValidityDays));
        var bytes = certificate.Export(X509ContentType.Pfx, password);
        try { return Publish(path, bytes, overwrite: false); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static bool Publish(string path, byte[] bytes, bool overwrite)
    {
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) { options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
            using (var stream = new FileStream(temporaryPath, options))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            try { File.Move(temporaryPath, path, overwrite); }
            catch (IOException) when (!overwrite && File.Exists(path))
            {
                // Another creator published first. The caller loads and validates that identity.
                return false;
            }
            return true;
        }
        finally { File.Delete(temporaryPath); }
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
