using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Moongate.Api.Tests.TestSupport.Security;

/// <summary>Runs scripts/api-certificates.sh into a temporary directory and loads what it produced.</summary>
internal sealed class ScriptedCertificates : IDisposable
{
    private const string PfxPassword = "test-only";

    private readonly string _directory;

    public string Directory => _directory;

    public ScriptedCertificates()
    {
        _directory = Path.Combine(Path.GetTempPath(), "moongate-api-certs-" + Guid.NewGuid().ToString("N"));
    }

    public static bool IsOpenSslAvailable()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";

        return path.Split(Path.PathSeparator).Any(directory => File.Exists(Path.Combine(directory, "openssl")));
    }

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

        return Path.Combine(directory.FullName, "scripts", "api-certificates.sh");
    }

    /// <summary>Runs the script with the given arguments and returns its exit code and combined output.</summary>
    public (int ExitCode, string Output) Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(ScriptPath());

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.ArgumentList.Add("--out");
        start.ArgumentList.Add(_directory);
        start.Environment["MOONGATE_PFX_PASSWORD"] = PfxPassword;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("bash did not start");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output);
    }

    public X509Certificate2 LoadRoot()
    {
        return X509CertificateLoader.LoadCertificateFromFile(Path.Combine(_directory, "ca.crt"));
    }

    public X509Certificate2 LoadLeaf(string name)
    {
        return X509Certificate2.CreateFromPemFile(
            Path.Combine(_directory, name + ".crt"),
            Path.Combine(_directory, name + ".key")
        );
    }

    public X509Certificate2 LoadPfx(string name)
    {
        return X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(_directory, name + ".pfx"), PfxPassword);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_directory))
        {
            System.IO.Directory.Delete(_directory, true);
        }
    }
}
