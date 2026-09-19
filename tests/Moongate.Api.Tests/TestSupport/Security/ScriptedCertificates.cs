using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Moongate.Api.Tests.TestSupport.Security;

/// <summary>Runs scripts/api-certificates.sh into a temporary directory and loads what it produced.</summary>
internal sealed class ScriptedCertificates : IDisposable
{
    private const string PfxPassword = "test-only";

    /// <summary>Gets the temporary directory the script writes into; deleted on dispose.</summary>
    public string OutputDirectory { get; }

    public ScriptedCertificates()
    {
        OutputDirectory = Path.Combine(Path.GetTempPath(), "moongate-api-certs-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>Returns whether both bash and openssl can be found on the PATH, which the script needs.</summary>
    public static bool IsOpenSslAvailable()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);

        return new[] { "openssl", "bash" }.All(tool => directories.Any(directory =>
            File.Exists(Path.Combine(directory, tool)) || File.Exists(Path.Combine(directory, tool + ".exe"))));
    }

    /// <summary>Locates the script by walking up from the test output directory to the repository root.</summary>
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

    /// <summary>Runs the script with the given arguments plus the output directory, returning its exit code and combined output.</summary>
    public async Task<(int ExitCode, string Output)> RunAsync(params string[] arguments)
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
        start.ArgumentList.Add(OutputDirectory);
        start.Environment["MOONGATE_PFX_PASSWORD"] = PfxPassword;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("bash did not start");
        process.StandardInput.Close();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await standardOutput + await standardError);
    }

    /// <summary>Loads ca.crt.</summary>
    public X509Certificate2 LoadRoot()
    {
        return X509CertificateLoader.LoadCertificateFromFile(Path.Combine(OutputDirectory, "ca.crt"));
    }

    /// <summary>Loads a leaf from its PEM certificate and key files.</summary>
    public X509Certificate2 LoadLeaf(string name)
    {
        return X509Certificate2.CreateFromPemFile(
            Path.Combine(OutputDirectory, name + ".crt"),
            Path.Combine(OutputDirectory, name + ".key")
        );
    }

    /// <summary>Loads a leaf from its PKCS#12 file with the password the script was given.</summary>
    public X509Certificate2 LoadPfx(string name)
    {
        return X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(OutputDirectory, name + ".pfx"), PfxPassword);
    }

    public void Dispose()
    {
        if (Directory.Exists(OutputDirectory))
        {
            Directory.Delete(OutputDirectory, true);
        }
    }
}
