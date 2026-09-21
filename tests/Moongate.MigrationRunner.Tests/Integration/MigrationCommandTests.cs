using System.Diagnostics;
using Moongate.MigrationRunner.Internal;
using Moongate.MigrationRunner.Tests.TestSupport;
using Npgsql;

namespace Moongate.MigrationRunner.Tests.Integration;

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
            await MigrationCommand.ExecuteAsync(
                ["status", "--root-directory", files.Root, "--target", "world", "--migrations-directory", files.Core],
                output,
                error
            )
        );
        Assert.DoesNotContain("sentinel-secret", output.ToString() + error);
    }

    [Fact]
    public async Task ExecuteAsync_ReadsExistingTomlAndOnlyResolvesSelectedTarget()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        var builder = new NpgsqlConnectionStringBuilder(db.ConnectionString);
        var uri =
            $"postgres://postgres@localhost/{builder.Database}?host={Uri.EscapeDataString(builder.Host!)}&pooling=false";
        var config =
            "[api]\nenabled = false\n[persistence.accounts]\nconnection_string = '$ABSENT_AUTH_DATABASE'\n[persistence.realm]\nconnection_string = '" +
            uri +
            "'\n";
        files.Write("config/moongate.toml", config);
        files.Write("migrations/world/0001_create.sql", "CREATE TABLE sample (value integer);");
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] options = ["--root-directory", files.Root, "--target", "world", "--migrations-directory", files.Core];
        Assert.Equal(0, await MigrationCommand.ExecuteAsync(["status", .. options], output, error));
        Assert.Contains("core/0001_create.sql", output.ToString());
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('sample') IS NOT NULL"));
        Assert.Equal(0, await MigrationCommand.ExecuteAsync(["apply", .. options], output, error));
        Assert.True(await db.ScalarAsync<bool>("SELECT to_regclass('sample') IS NOT NULL"));
        Assert.Equal(string.Empty, error.ToString());
        Assert.False(File.Exists(Path.Combine(files.Root, "moongate.pid")));
    }

    [Theory, InlineData(""), InlineData("apply"), InlineData("apply --target both")]
    public async Task ExecuteAsync_RequiresExplicitCommandAndSingleTarget(string arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(
            1,
            await MigrationCommand.ExecuteAsync(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries), output, error)
        );
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
