using System.Diagnostics;
using Moongate.Core.Utils;
using Moongate.Server.Data.Config;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Plugins;

namespace Moongate.Tests.Integration.Persistence;

[Collection(PostgresTestCollection.Name)]
public sealed class PersistenceSchemaCliTests
{
    [Fact]
    public async Task Help_ShowsSchemaOptionWithoutComposingHost()
    {
        using var files = new PluginDirectoryFixture();
        var result = await RunAsync(files.Directories.Root, "preview", null, true);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--persistence-schema", result.Output);
        Assert.False(Directory.Exists(Path.Combine(files.Directories.Root, "config")));
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

    [Theory, InlineData("PersistencePlugin"), InlineData("SamplePlugin")]
    public async Task PreviewThenGenerate_ActualCliLoadsDiskPlugin_WithoutNormalHostComposition(
        string bundle
    )
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var files = new PluginDirectoryFixture("plugins", "config");
        files.Deploy(bundle);
        var table = bundle == "SamplePlugin" ? "sample_greeter.notes" : "fixture_data.items";
        var module = bundle == "SamplePlugin" ? "moongate.auto.realm." : "fixture.persistenceplugin";

        // Normal startup would fail immediately on this guard and require Ultima data later.
        using var pid = PidFileGuard.Acquire(files.Directories.Root);
        var preview = await RunAsync(files.Directories.Root, "preview", database.ConnectionString);
        Assert.True(preview.ExitCode == 0, preview.Output);
        Assert.Contains(module, preview.Output);
        Assert.Contains("CREATE TABLE", preview.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Moongate Server starting", preview.Output);
        Assert.False(await database.ScalarAsync<bool>($"SELECT to_regclass('{table}') IS NOT NULL"));
        var path = Path.Combine(files.Directories.Root, "migrations", "world", "0001_create.sql");
        var generate = await RunAsync(files.Directories.Root, "generate", database.ConnectionString, migrationOutput: path);
        Assert.True(generate.ExitCode == 0, generate.Output);
        Assert.False(await database.ScalarAsync<bool>($"SELECT to_regclass('{table}') IS NOT NULL"));
        await database.ExecuteAsync(await File.ReadAllTextAsync(path));
        var unchanged = await RunAsync(files.Directories.Root, "preview", database.ConnectionString);
        Assert.Equal(0, unchanged.ExitCode);
        Assert.Contains("No PostgreSQL schema changes", unchanged.Output);
        Assert.False(Directory.Exists(Path.Combine(files.Directories.Root, "logs")));
        Assert.False(Directory.Exists(Path.Combine(files.Directories.Root, "scripts")));
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

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string root,
        string mode,
        string? connection,
        bool help = false,
        string? migrationOutput = null
    )
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            ArgumentList =
                { typeof(MoongateServerBootstrap).Assembly.Location, "--root-directory", root, "--persistence-schema", mode }
        };

        if (migrationOutput is not null)
        {
            start.ArgumentList.Add("--migration-output");
            start.ArgumentList.Add(migrationOutput);
            start.ArgumentList.Add("--migration-target");
            start.ArgumentList.Add("world");
        }

        if (help)
        {
            start.ArgumentList.Clear();
            start.ArgumentList.Add(typeof(MoongateServerBootstrap).Assembly.Location);
            start.ArgumentList.Add("--help");
        }

        if (!help && mode != "invalid")
        {
            var configDirectory = Path.Combine(root, "config");
            Directory.CreateDirectory(configDirectory);
            var configPath = Path.Combine(configDirectory, "moongate.toml");
            var config = File.Exists(configPath)
                ? TomlUtils.DeserializeFromFile<MoongateServerConfig>(configPath)!
                : new MoongateServerConfig();
            config.Persistence.Realm.ConnectionString = "$MOONGATE_REALM_DATABASE";
            TomlUtils.SerializeToFile(config, configPath);
        }

        start.Environment.Remove("MOONGATE_REALM_DATABASE");
        start.Environment.Remove("MOONGATE_ACCOUNTS_DATABASE");

        if (connection is not null)
        {
            start.Environment["MOONGATE_REALM_DATABASE"] = connection;
        }

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
            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
        }
    }
}
