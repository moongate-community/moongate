using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Core.Utils;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Server.Data.Config;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Bootstrap.Setup;

public sealed class AdminCertificateSetupTests
{
    [Fact]
    public void Configure_NewCertificate_EnablesTlsAndPreservesOtherConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var configPath = CreateConfig(directory.Path);
        AdminCertificateSetup.Configure(directory.Path, ["login.example.test", "192.0.2.10"], TextWriter.Null);
        var text = File.ReadAllText(configPath);
        var config = TomlUtils.Deserialize<MoongateServerConfig>(text)!;
        Assert.True(config.AdminApi.Enabled);
        Assert.False(config.AdminApi.AllowInsecureLoopback);
        Assert.Equal("*", config.AdminApi.ListenAddress);
        Assert.Equal(2599, config.AdminApi.Port);
        Assert.Equal("certificates/admin.pfx", config.AdminApi.CertificatePath);
        Assert.Equal("", config.AdminApi.CertificatePassword);
        Assert.Contains("# preserve this comment", text);
        Assert.Contains("custom_value = 'unchanged'", text);
        Assert.Contains("# keep port", text);
        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            Path.Combine(directory.Path, "certificates/admin.pfx"),
            ""
        );
        using var exported =
            X509CertificateLoader.LoadCertificateFromFile(Path.Combine(directory.Path, "certificates/admin.crt"));
        Assert.True(certificate.HasPrivateKey);
        Assert.False(exported.HasPrivateKey);
        Assert.Equal(certificate.Thumbprint, exported.Thumbprint);

        foreach (var host in new[] { "localhost", "127.0.0.1", "::1", "login.example.test", "192.0.2.10" })
        {
            Assert.True(certificate.MatchesHostname(host, false, false), host);
        }

        Assert.True(certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(360));
        Assert.False(certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
        Assert.Contains(
            certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages.Cast<Oid>(),
            oid => oid.Value == "1.3.6.1.5.5.7.3.1"
        );

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(directory.Path, "certificates/admin.pfx"))
            );
        }
    }

    [Theory, InlineData("login.example.test"), InlineData("münchen.example.test")]
    public void Configure_RepeatedRun_ReusesIdentityAndConfigBytes(string host)
    {
        using var directory = new TemporaryDirectory();
        var configPath = CreateConfig(directory.Path);
        AdminCertificateSetup.Configure(directory.Path, [host], TextWriter.Null);
        var originalConfig = File.ReadAllBytes(configPath);
        var originalPfx = File.ReadAllBytes(Path.Combine(directory.Path, "certificates/admin.pfx"));
        AdminCertificateSetup.Configure(directory.Path, [host], TextWriter.Null);
        Assert.Equal(originalConfig, File.ReadAllBytes(configPath));
        Assert.Equal(originalPfx, File.ReadAllBytes(Path.Combine(directory.Path, "certificates/admin.pfx")));
    }

    [Theory, InlineData("*"), InlineData("https://login.example.test"), InlineData("bad name"), InlineData("0.0.0.0"),
     InlineData("::")]
    public void Configure_InvalidHost_DoesNotChangeConfigOrCreateCertificate(string host)
    {
        using var directory = new TemporaryDirectory();
        var configPath = CreateConfig(directory.Path);
        var original = File.ReadAllBytes(configPath);
        Assert.Throws<ArgumentException>(() => AdminCertificateSetup.Configure(directory.Path, [host], TextWriter.Null));
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "certificates")));
    }

    [Fact]
    public void Configure_IncompleteExistingCertificate_PreservesFilesAndConfig()
    {
        using var directory = new TemporaryDirectory();
        var configPath = CreateConfig(directory.Path);
        var original = File.ReadAllBytes(configPath);
        Directory.CreateDirectory(Path.Combine(directory.Path, "certificates"));
        var pfxPath = Path.Combine(directory.Path, "certificates/admin.pfx");
        File.WriteAllText(pfxPath, "existing material");
        Assert.Throws<InvalidOperationException>(() => AdminCertificateSetup.Configure(directory.Path, [], TextWriter.Null));
        Assert.Equal("existing material", File.ReadAllText(pfxPath));
        Assert.Equal(original, File.ReadAllBytes(configPath));
    }

    [Fact]
    public void Configure_NewHostOnExistingCertificate_RefusesImplicitRotation()
    {
        using var directory = new TemporaryDirectory();
        var configPath = CreateConfig(directory.Path);
        AdminCertificateSetup.Configure(directory.Path, [], TextWriter.Null);
        var original = File.ReadAllBytes(configPath);
        Assert.Throws<InvalidOperationException>(() =>
            AdminCertificateSetup.Configure(directory.Path, ["new.example.test"], TextWriter.Null)
        );
        Assert.Equal(original, File.ReadAllBytes(configPath));
    }

    [Theory, InlineData("[invalid"), InlineData("[admin_api]\ncertificate_path = 'custom.pfx'")]
    public void Configure_InvalidOrCustomConfig_DoesNotPublishCertificate(string text)
    {
        using var directory = new TemporaryDirectory();
        var path = CreateConfig(directory.Path);
        File.WriteAllText(path, text);
        Assert.ThrowsAny<Exception>(() => AdminCertificateSetup.Configure(directory.Path, [], TextWriter.Null));
        Assert.Equal(text, File.ReadAllText(path));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "certificates")));
    }

    [Fact]
    public void Configure_MismatchedPublicCertificate_PreservesPairAndConfig()
    {
        using var directory = new TemporaryDirectory();
        var path = CreateConfig(directory.Path);
        AdminCertificateSetup.Configure(directory.Path, [], TextWriter.Null);
        var config = File.ReadAllBytes(path);
        var pfx = File.ReadAllBytes(Path.Combine(directory.Path, "certificates/admin.pfx"));
        using var other = new TemporaryDirectory();
        CreateConfig(other.Path);
        AdminCertificateSetup.Configure(other.Path, [], TextWriter.Null);
        var wrongPublic = File.ReadAllText(Path.Combine(other.Path, "certificates/admin.crt"));
        File.WriteAllText(Path.Combine(directory.Path, "certificates/admin.crt"), wrongPublic);
        Assert.Throws<InvalidOperationException>(() => AdminCertificateSetup.Configure(directory.Path, [], TextWriter.Null));
        Assert.Equal(config, File.ReadAllBytes(path));
        Assert.Equal(pfx, File.ReadAllBytes(Path.Combine(directory.Path, "certificates/admin.pfx")));
        Assert.Equal(wrongPublic, File.ReadAllText(Path.Combine(directory.Path, "certificates/admin.crt")));
    }

    [Theory, InlineData(false, true, false), InlineData(true, false, false), InlineData(true, true, true)]
    public void Configure_IncompatibleExistingIdentity_DoesNotEnableApi(bool serverUsage, bool signatureUsage, bool expired)
    {
        using var directory = new TemporaryDirectory();
        var configPath = CreateConfig(directory.Path);
        var originalConfig = File.ReadAllBytes(configPath);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new(serverUsage ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") },
                true
            )
        );
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                signatureUsage ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment,
                true
            )
        );
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        san.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1)
        );
        Directory.CreateDirectory(Path.Combine(directory.Path, "certificates"));
        var pfxPath = Path.Combine(directory.Path, "certificates/admin.pfx");
        var originalPfx = certificate.Export(X509ContentType.Pfx, "");
        File.WriteAllBytes(pfxPath, originalPfx);
        File.WriteAllText(Path.Combine(directory.Path, "certificates/admin.crt"), certificate.ExportCertificatePem());
        Assert.Throws<InvalidOperationException>(() => AdminCertificateSetup.Configure(directory.Path, [], TextWriter.Null));
        Assert.Equal(originalConfig, File.ReadAllBytes(configPath));
        Assert.Equal(originalPfx, File.ReadAllBytes(pfxPath));
    }

    private static string CreateConfig(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "config"));
        var path = Path.Combine(root, "config/moongate.toml");
        File.WriteAllText(
            path,
            """
            # preserve this comment
            custom_value = 'unchanged'
            [admin_api]
            enabled = false # keep enabled comment
            listen_address = '*'
            port = 2599 # keep port
            certificate_path = ''
            certificate_password = ''
            """
        );

        return path;
    }
}
