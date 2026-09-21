using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Moongate.Core.Directories;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api.Internal;
using Moongate.Tests.TestSupport.Api;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Environment;
using Moongate.Tests.TestSupport.Scripting;
using Serilog;

namespace Moongate.Tests.Integration.Api;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class ApiCertificateStoreTests
{
    [Fact]
    public void Load_MissingCertificate_GeneratesPasswordlessIdentityWithConfiguredNamesAndPublicCopy()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config"]);
        var config = GenerationConfig();
        config.CertificateDnsNames = ["realm.internal"];
        config.CertificateIpAddresses = ["10.0.0.12", "::1"];
        using var certificate = new ApiCertificateStore().Load(config, directories, TimeProvider.System);
        Assert.True(certificate.HasPrivateKey);
        Assert.Equal(certificate.Subject, certificate.Issuer);
        Assert.True(certificate.MatchesHostname("realm.internal", false, false));
        Assert.True(certificate.MatchesHostname("10.0.0.12", false, false));
        Assert.True(certificate.MatchesHostname("::1", false, false));
        Assert.False(certificate.MatchesHostname("localhost", false, false));
        Assert.InRange(
            certificate.NotAfter.ToUniversalTime() - DateTime.UtcNow,
            TimeSpan.FromDays(364),
            TimeSpan.FromDays(366)
        );
        Assert.False(Assert.Single(certificate.Extensions.OfType<X509BasicConstraintsExtension>()).CertificateAuthority);
        var usages = Assert.Single(certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>())
            .EnhancedKeyUsages.Cast<Oid>()
            .Select(oid => oid.Value);
        Assert.Contains("1.3.6.1.5.5.7.3.1", usages);
        Assert.Contains("1.3.6.1.5.5.7.3.2", usages);
        var path = Path.Combine(directories["config"], config.CertificatePath);
        using var persisted = X509CertificateLoader.LoadPkcs12FromFile(path, null, X509KeyStorageFlags.EphemeralKeySet);
        using var publicCertificate = X509CertificateLoader.LoadCertificateFromFile(path + ".pem");
        Assert.True(persisted.HasPrivateKey);
        Assert.False(publicCertificate.HasPrivateKey);
        Assert.Equal(certificate.RawData, persisted.RawData);
        Assert.Equal(certificate.RawData, publicCertificate.RawData);
        Assert.DoesNotContain("PRIVATE KEY", File.ReadAllText(path + ".pem"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(Path.GetDirectoryName(path)!)
            );
        }
    }

    [Fact]
    public void Load_RestartAndMissingPublicCopy_PreservesPrivateIdentity()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config"]);
        var config = GenerationConfig();
        var store = new ApiCertificateStore();
        using var first = store.Load(config, directories, TimeProvider.System);
        var path = Path.Combine(directories["config"], config.CertificatePath);
        var bytes = File.ReadAllBytes(path);
        File.Delete(path + ".pem");
        config.CertificateDnsNames = ["changed.internal"];
        using var second = store.Load(config, directories, TimeProvider.System);
        using var publicCertificate = X509CertificateLoader.LoadCertificateFromFile(path + ".pem");
        Assert.Equal(first.RawData, second.RawData);
        Assert.Equal(first.RawData, publicCertificate.RawData);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.True(second.MatchesHostname("localhost", false, false));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Load_ConcurrentCreators_PublishOneCompleteIdentity(bool repairPublicCopy)
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config"]);
        var config = GenerationConfig();
        if (repairPublicCopy)
        {
            using var existing = new ApiCertificateStore().Load(config, directories, TimeProvider.System);
            // Also exercise simultaneous reads/replacement of an existing export on Windows.
            File.WriteAllText(Path.Combine(directories["config"], config.CertificatePath) + ".pem", "stale public copy");
        }

        using var ready = new CountdownEvent(4);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(async () =>
                    {
                        ready.Signal();
                        await start.Task;
                        using var certificate = new ApiCertificateStore().Load(config, directories, TimeProvider.System);
                        return certificate.GetCertHashString(HashAlgorithmName.SHA256);
                    }
                )
            )
            .ToArray();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
        start.SetResult();
        var identities = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(identities.Distinct());
        var files = Directory.GetFiles(Path.Combine(directories["config"], "tls"));
        Assert.Equal(2, files.Length);
        using var publicCertificate =
            X509CertificateLoader.LoadCertificateFromFile(
                Path.Combine(directories["config"], config.CertificatePath) + ".pem"
            );
        Assert.Equal(identities[0], publicCertificate.GetCertHashString(HashAlgorithmName.SHA256));
    }

    [Fact]
    public void Load_SeparateDirectories_GeneratesDistinctIdentities()
    {
        using var firstDirectory = new TemporaryDirectory();
        using var secondDirectory = new TemporaryDirectory();
        using var first = new ApiCertificateStore().Load(
            GenerationConfig(),
            new DirectoriesConfig(firstDirectory.Path, ["config"]),
            TimeProvider.System
        );
        using var second = new ApiCertificateStore().Load(
            GenerationConfig(),
            new DirectoriesConfig(secondDirectory.Path, ["config"]),
            TimeProvider.System
        );
        Assert.NotEqual(
            first.GetCertHashString(HashAlgorithmName.SHA256),
            second.GetCertHashString(HashAlgorithmName.SHA256)
        );
    }

    [Theory, InlineData("corrupt"), InlineData("expired"), InlineData("future"), InlineData("client_only"),
     InlineData("no_private_key"), InlineData("wrong_password")]
    public void Load_ExistingInvalidCertificate_FailsWithoutReplacingIt(string invalidity)
    {
        using var fixture = new ApiHostFixture();
        fixture.Config.AutoGenerateCertificate = true;
        var path = Path.Combine(fixture.Directories["config"], fixture.Config.CertificatePath);
        if (invalidity == "corrupt")
        {
            File.WriteAllText(path, "invalid test certificate");
        }
        else if (invalidity == "wrong_password")
        {
            Environment.SetEnvironmentVariable(fixture.Config.CertificatePasswordEnvironmentVariable, "wrong-test-password");
        }
        else
        {
            fixture.UseInvalidServerCertificate(invalidity);
        }

        var bytes = File.ReadAllBytes(path);
        Assert.ThrowsAny<Exception>(() => new ApiCertificateStore().Load(
                fixture.Config,
                fixture.Directories,
                TimeProvider.System
            )
        );
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".pem"));
    }

    [Fact]
    public void Load_GenerationDisabled_DoesNotCreateMissingIdentity()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config"]);
        var config = GenerationConfig();
        config.AutoGenerateCertificate = false;
        Assert.ThrowsAny<Exception>(() => new ApiCertificateStore().Load(config, directories, TimeProvider.System));
        Assert.False(Directory.Exists(Path.Combine(directories["config"], "tls")));
    }

    [Fact]
    public void Load_ConfiguredPassword_EncryptsGeneratedPfx()
    {
        using var fixture = new ApiHostFixture();
        var path = Path.Combine(fixture.Directories["config"], fixture.Config.CertificatePath);
        File.Delete(path);
        fixture.Config.AutoGenerateCertificate = true;
        using var certificate = new ApiCertificateStore().Load(fixture.Config, fixture.Directories, TimeProvider.System);
        Assert.Throws<CryptographicException>(() => X509CertificateLoader.LoadPkcs12FromFile(path, null));
        using var persisted = X509CertificateLoader.LoadPkcs12FromFile(
            path,
            fixture.Password,
            X509KeyStorageFlags.EphemeralKeySet
        );
        Assert.Equal(certificate.RawData, persisted.RawData);
    }

    [Fact]
    public void Load_MissingConfiguredPassword_FailsBeforeCreatingFiles()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config"]);
        var config = GenerationConfig();
        config.CertificatePasswordEnvironmentVariable = $"MOONGATE_TEST_ABSENT_{Guid.NewGuid():N}";
        Assert.Throws<InvalidOperationException>(() => new ApiCertificateStore().Load(
                config,
                directories,
                TimeProvider.System
            )
        );
        Assert.False(Directory.Exists(Path.Combine(directories["config"], "tls")));
    }

    [Fact]
    public void Load_PublicExportFailure_RetryKeepsPersistedPrivateIdentity()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config"]);
        var config = GenerationConfig();
        var path = Path.Combine(directories["config"], config.CertificatePath);
        Directory.CreateDirectory(path + ".pem");
        Assert.ThrowsAny<IOException>(() => new ApiCertificateStore().Load(config, directories, TimeProvider.System));
        var bytes = File.ReadAllBytes(path);
        Directory.Delete(path + ".pem");
        using var certificate = new ApiCertificateStore().Load(config, directories, TimeProvider.System);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        using var publicCertificate = X509CertificateLoader.LoadCertificateFromFile(path + ".pem");
        Assert.Equal(certificate.RawData, publicCertificate.RawData);
        Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(path)!).Length);
    }

    [Fact]
    public void Load_GeneratedIdentity_LogsOnlyPublicMetadata()
    {
        using var fixture = new ApiHostFixture();
        var path = Path.Combine(fixture.Directories["config"], fixture.Config.CertificatePath);
        File.Delete(path);
        fixture.Config.AutoGenerateCertificate = true;
        var sink = new CapturingLogSink();
        var previous = Log.Logger;
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        Log.Logger = logger;
        try
        {
            using var certificate = new ApiCertificateStore().Load(fixture.Config, fixture.Directories, TimeProvider.System);
            var messages = string.Join('\n', sink.Events.Select(entry => entry.RenderMessage()));
            Assert.Contains(path, messages);
            Assert.Contains(path + ".pem", messages);
            Assert.Contains(certificate.GetCertHashString(HashAlgorithmName.SHA256), messages);
            Assert.Contains(sink.Events, entry => entry.Properties.ContainsKey("ExpiresAt"));
            Assert.DoesNotContain(fixture.Password, messages);
            Assert.DoesNotContain("PRIVATE KEY", messages);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    private static ApiConfig GenerationConfig()
        => new()
        {
            AutoGenerateCertificate = true,
            CertificatePath = "tls/server.pfx",
            CertificatePasswordEnvironmentVariable = ""
        };
}
