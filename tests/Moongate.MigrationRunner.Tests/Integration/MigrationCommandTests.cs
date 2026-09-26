using System.Diagnostics;
using Moongate.MigrationRunner.Internal;
using Moongate.MigrationRunner.Tests.TestSupport;
using Moongate.Persistence.Migrations.Types.Migrations;
using Npgsql;

namespace Moongate.MigrationRunner.Tests.Integration;

[Collection(PostgresTestCollection.Name)]
public sealed class MigrationCommandTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _postgres;

    public MigrationCommandTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task ExecuteAsync_InvalidConnectionDoesNotExposeCredentials()
    {
        using var files = new MigrationFiles();
        files.Write(
            "config/moongate.toml",
            "[persistence.realm]\nconnection_string = 'postgres://user:sentinel-secret@host/db?invalid_option=sentinel-secret'\n"
        );
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(
            1,
            await MigrationCommand.StatusAsync(MigrationTarget.World, files.Root, files.Core, null, output, error)
        );
        Assert.DoesNotContain("sentinel-secret", output.ToString() + error);
    }

    [Fact]
    public async Task ExecuteAsync_ReadsExistingTomlAndOnlyResolvesSelectedTarget()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        var builder = new NpgsqlConnectionStringBuilder(db.ConnectionString);

        // PostgreSqlConnectionString.Normalize reads the port only from the URI's own authority
        // (defaulting to 5432 there), never from a query parameter, so a non-default port must ride
        // in the authority itself - "localhost" alone silently assumed 5432 whenever the real
        // Postgres this test runs against listens elsewhere.
        // The credentials ride in the authority too: a Testcontainers server requires the password,
        // while the CI service trusts the connection and has none.
        var userInfo = Uri.EscapeDataString(builder.Username ?? "postgres") +
                       (string.IsNullOrEmpty(builder.Password) ? "" : ":" + Uri.EscapeDataString(builder.Password));
        var uri =
            $"postgres://{userInfo}@localhost:{builder.Port}/{builder.Database}?host={Uri.EscapeDataString(builder.Host!)}&pooling=false";
        var config =
            "[persistence.accounts]\nconnection_string = '$ABSENT_AUTH_DATABASE'\n[persistence.realm]\nconnection_string = '" +
            uri +
            "'\n";
        files.Write("config/moongate.toml", config);
        files.Write("migrations/world/0001_create.sql", "CREATE TABLE sample (value integer);");
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(
            0,
            await MigrationCommand.StatusAsync(MigrationTarget.World, files.Root, files.Core, null, output, error)
        );
        Assert.Contains("core/0001_create.sql", output.ToString());
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('sample') IS NOT NULL"));
        Assert.Equal(
            0,
            await MigrationCommand.ApplyAsync(MigrationTarget.World, files.Root, files.Core, null, output, error)
        );
        Assert.True(await db.ScalarAsync<bool>("SELECT to_regclass('sample') IS NOT NULL"));
        Assert.Equal(string.Empty, error.ToString());
        Assert.False(File.Exists(Path.Combine(files.Root, "moongate.pid")));
    }

    [Fact]
    public async Task ReleasedLayout_DefaultRootUsesServerConfigurationAndPlugins()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("config/moongate.toml", "[persistence.realm]\nconnection_string = '" + db.ConnectionString + "'\n");
        files.Write("plugins/p/migrations/manifest.json", "{\"id\":\"sample\"}");
        files.Write("plugins/p/migrations/world/0001_data.sql", "SELECT 1;");
        var runnerDirectory = Path.Combine(files.Root, "migration-runner");
        Directory.CreateDirectory(runnerDirectory);

        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
        {
            if (Path.GetExtension(file) is ".dll" or ".json")
            {
                File.Copy(file, Path.Combine(runnerDirectory, Path.GetFileName(file)));
            }
        }

        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { Path.Combine(runnerDirectory, "Moongate.MigrationRunner.dll"), "status", "--target", "world" }
        };
        start.Environment.Remove("MOONGATE_ROOT");
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(process.ExitCode == 0, await stderr);
            Assert.Contains("sample/0001_data.sql", await stdout);
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
