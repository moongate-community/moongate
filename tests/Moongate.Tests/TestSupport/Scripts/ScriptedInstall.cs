using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Moongate.Tests.TestSupport.Scripts;

/// <summary>Publishes fake releases into a temporary directory and runs scripts/install.sh against them.</summary>
internal sealed class ScriptedInstall : IDisposable
{
    private readonly string _root;

    /// <summary>Gets the directory the script downloads from, laid out the way the release URLs are.</summary>
    public string ReleaseDirectory { get; }

    /// <summary>Gets the directory the script installs into.</summary>
    public string InstallDirectory { get; }

    /// <summary>Gets the directory the script links the command into.</summary>
    public string BinDirectory { get; }

    public ScriptedInstall()
    {
        _root = Path.Combine(Path.GetTempPath(), "moongate-install-" + Guid.NewGuid().ToString("N"));
        ReleaseDirectory = Path.Combine(_root, "releases");
        InstallDirectory = Path.Combine(_root, "opt", "moongate");
        BinDirectory = Path.Combine(_root, "bin");
        Directory.CreateDirectory(ReleaseDirectory);
        Directory.CreateDirectory(BinDirectory);
    }

    /// <summary>Returns whether this is Linux and the tools the script needs are on the PATH.</summary>
    public static bool AreToolsAvailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var directories = (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);

        return new[] { "sh", "tar", "curl", "sha256sum" }
            .All(tool => directories.Any(directory => File.Exists(Path.Combine(directory, tool))));
    }

    /// <summary>Locates scripts/install.sh by walking up from the test output directory to the repository root.</summary>
    public static string ScriptPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
        }

        return Path.Combine(directory.FullName, "scripts", "install.sh");
    }

    /// <summary>Writes a release archive and its checksum, with the given text standing in for the server binary.</summary>
    public void Publish(string version, string rid, string binaryContent)
    {
        var bundle = Path.Combine(_root, "staging-" + Guid.NewGuid().ToString("N"), "moongate-" + rid);
        Directory.CreateDirectory(bundle);
        File.WriteAllText(Path.Combine(bundle, "Moongate.Server"), binaryContent);
        File.WriteAllText(Path.Combine(bundle, "LICENSE"), "GNU AFFERO GENERAL PUBLIC LICENSE");
        var directory = Path.Combine(ReleaseDirectory, "v" + version);
        Directory.CreateDirectory(directory);
        var archive = ArchivePath(version, rid);

        using (var file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        {
            TarFile.CreateFromDirectory(Path.GetDirectoryName(bundle)!, gzip, includeBaseDirectory: false);
        }

        using var stream = File.OpenRead(archive);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        File.WriteAllText(archive + ".sha256", hash + "  " + Path.GetFileName(archive) + "\n");
    }

    /// <summary>Replaces the archive's bytes and leaves its checksum file untouched.</summary>
    public void Corrupt(string version, string rid)
    {
        File.WriteAllText(ArchivePath(version, rid), "not an archive");
    }

    /// <summary>Runs the script against the fake release, returning its exit code and combined output.</summary>
    public async Task<(int ExitCode, string Output)> RunAsync(string version, string rid)
    {
        var start = new ProcessStartInfo("sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(ScriptPath());
        start.Environment["MOONGATE_VERSION"] = version;
        start.Environment["MOONGATE_RID"] = rid;
        start.Environment["MOONGATE_BASE_URL"] = new Uri(ReleaseDirectory + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');
        start.Environment["MOONGATE_INSTALL_DIR"] = InstallDirectory;
        start.Environment["MOONGATE_BIN_DIR"] = BinDirectory;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("sh did not start");
        process.StandardInput.Close();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await standardOutput + await standardError);
    }

    private string ArchivePath(string version, string rid)
    {
        return Path.Combine(ReleaseDirectory, "v" + version, $"moongate-{rid}-{version}.tar.gz");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
