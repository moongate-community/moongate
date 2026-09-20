using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Plugins;

namespace Moongate.Tests.Integration.Persistence;

public sealed class PersistenceSchemaCliTests
{
    [Theory, InlineData(false, "PersistencePlugin"), InlineData(true, "PersistencePlugin"), InlineData(false, "SamplePlugin")]
    public async Task PreviewThenApply_ActualCliLoadsDiskPlugin_WithoutNormalHostComposition(bool autoGenerateCertificate, string bundle)
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var files = new PluginDirectoryFixture("plugins", "config");
        files.Deploy(bundle);
        var table = bundle == "SamplePlugin" ? "sample_greeter.notes" : "fixture_data.items";
        var module = bundle == "SamplePlugin" ? "com.github.moongate-community.moongate.plugins.greeter" : "fixture.persistenceplugin";
        // Normal startup would fail immediately on this guard and require a certificate/Ultima data later.
        using var pid = PidFileGuard.Acquire(files.Directories.Root);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await File.WriteAllTextAsync(Path.Combine(files.Directories["config"], "moongate.toml"), $"""
            [api]
            enabled = true
            listen_address = "127.0.0.1"
            port = {port}
            auto_generate_certificate = {autoGenerateCertificate.ToString().ToLowerInvariant()}
            certificate_path = "certificates/schema-must-not-create.pfx"
            trusted_root_paths = ["missing-root.crt"]
            [[api.peers]]
            certificate_sha256 = "{new string('A', 64)}"
            peer_id = "schema-test"
            """);
        var preview = await RunAsync(files.Directories.Root, "preview", database.ConnectionString);
        Assert.True(preview.ExitCode == 0, preview.Output);
        Assert.Contains(module, preview.Output);
        Assert.Contains("CREATE TABLE", preview.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Moongate Server starting", preview.Output);
        Assert.False(await database.ScalarAsync<bool>($"SELECT to_regclass('{table}') IS NOT NULL"));
        var apply = await RunAsync(files.Directories.Root, "apply", database.ConnectionString);
        Assert.True(apply.ExitCode == 0, apply.Output);
        Assert.True(await database.ScalarAsync<bool>($"SELECT to_regclass('{table}') IS NOT NULL"));
        var unchanged = await RunAsync(files.Directories.Root, "preview", database.ConnectionString);
        Assert.Equal(0, unchanged.ExitCode);
        Assert.Contains("No PostgreSQL schema changes", unchanged.Output);
        Assert.False(Directory.Exists(Path.Combine(files.Directories.Root, "logs")));
        Assert.False(Directory.Exists(Path.Combine(files.Directories.Root, "scripts")));
        Assert.Empty(Directory.EnumerateFiles(files.Directories.Root, "*.pfx", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Preview_MissingEnvironment_ReportsTargetAndVariableWithoutStartingHost()
    {
        using var files = new PluginDirectoryFixture("plugins", "config");
        files.Deploy("PersistencePlugin");
        var result = await RunAsync(files.Directories.Root, "preview", null);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("MOONGATE_REALM_DATABASE", result.Output);
        Assert.Contains("Realm", result.Output);
        Assert.False(File.Exists(Path.Combine(files.Directories.Root, "moongate.pid")));
    }

    [Fact]
    public async Task InvalidMode_ReportsUsageWithoutCreatingHostFiles()
    {
        using var files = new PluginDirectoryFixture();
        var result = await RunAsync(files.Directories.Root, "invalid", null);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("persistence", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(files.Directories.Root, "config")));
    }

    [Fact]
    public async Task Help_ShowsSchemaOptionWithoutComposingHost()
    {
        using var files = new PluginDirectoryFixture();
        var result = await RunAsync(files.Directories.Root, "preview", null, help: true);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--persistence-schema", result.Output);
        Assert.False(Directory.Exists(Path.Combine(files.Directories.Root, "config")));
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string root, string mode, string? connection, bool help = false)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            ArgumentList = { typeof(MoongateServerBootstrap).Assembly.Location, "--root-directory", root, "--persistence-schema", mode }
        };
        if (help)
        {
            start.ArgumentList.Clear();
            start.ArgumentList.Add(typeof(MoongateServerBootstrap).Assembly.Location);
            start.ArgumentList.Add("--help");
        }
        start.Environment.Remove("MOONGATE_REALM_DATABASE");
        start.Environment.Remove("MOONGATE_ACCOUNTS_DATABASE");
        if (connection is not null) { start.Environment["MOONGATE_REALM_DATABASE"] = connection; }
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            return (process.ExitCode, await stdout + await stderr);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
        }
    }
}
