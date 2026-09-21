using System.Diagnostics;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Serilog;

namespace Moongate.Server.Bootstrap.Internal;

internal sealed class DevelopmentMigrationRunner : IDevelopmentMigrationRunner
{
    private readonly string _root;
    private readonly string _migrations;
    private readonly string? _plugins;
    private readonly string _runnerDirectory;
    private readonly ILogger _logger = Log.ForContext<DevelopmentMigrationRunner>();

    public DevelopmentMigrationRunner(string root, string migrations, string? plugins, string? runnerDirectory = null)
    {
        _root = Path.GetFullPath(root);
        _migrations = Path.GetFullPath(migrations);
        _plugins = plugins is null ? null : Path.GetFullPath(plugins);
        _runnerDirectory = runnerDirectory ?? Path.Combine(AppContext.BaseDirectory, "migration-runner");
    }

    public void ValidateAvailable()
    {
        if (!File.Exists(ExecutablePath) && !File.Exists(AssemblyPath))
        {
            throw new InvalidOperationException(
                $"The isolated migration runner is missing from '{_runnerDirectory}'. Build or restore the migration-runner bundle."
            );
        }
    }

    public async Task ApplyAsync(PersistenceDatabaseTarget target, CancellationToken cancellationToken)
    {
        ValidateAvailable();
        cancellationToken.ThrowIfCancellationRequested();
        var useExecutable = File.Exists(ExecutablePath);
        var info = new ProcessStartInfo(useExecutable ? ExecutablePath : "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (!useExecutable)
        {
            info.ArgumentList.Add(AssemblyPath);
        }

        foreach (var argument in new[]
                 {
                     "apply", "--target", target == PersistenceDatabaseTarget.Accounts ? "auth" : "world",
                     "--root-directory", _root, "--migrations-directory", _migrations
                 })
        {
            info.ArgumentList.Add(argument);
        }

        if (_plugins is not null)
        {
            info.ArgumentList.Add("--plugins-directory");
            info.ArgumentList.Add(_plugins);
        }

        using var process = Process.Start(info) ??
                            throw new InvalidOperationException("Could not start the migration runner.");
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Do not release startup locks with an abandoned runner still executing DDL.
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            throw;
        }

        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Migration runner failed for {target}: {error.Trim()}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _logger.Information("Development migrations for {Target}: {Result}", target, output.Trim());
    }

    private string ExecutablePath => Path.Combine(
        _runnerDirectory,
        OperatingSystem.IsWindows() ? "Moongate.MigrationRunner.exe" : "Moongate.MigrationRunner"
    );

    private string AssemblyPath => Path.Combine(_runnerDirectory, "Moongate.MigrationRunner.dll");
}
