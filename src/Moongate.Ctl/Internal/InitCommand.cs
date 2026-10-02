using System.Diagnostics;
using ConsoleAppFramework;
using Moongate.Core.Utils;

namespace Moongate.Ctl.Internal;

internal static class InitCommand
{
    /// <summary>
    ///     Prepare a server root offline using the matching mgserver distribution.
    /// </summary>
    /// <param name="rootDirectory">
    ///     Root directory to initialize.
    /// </param>
    /// <param name="generateAdminCertificate">
    ///     Create or reuse a TLS certificate and enable the administration API.
    /// </param>
    /// <param name="adminCertificateHosts">
    ///     Comma-separated additional DNS names or IP addresses for the certificate.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancellation for the initialization process.
    /// </param>
    public static async Task<int> RunAsync(
        [Argument] string rootDirectory,
        bool generateAdminCertificate = false,
        string? adminCertificateHosts = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || adminCertificateHosts is not null && !generateAdminCertificate)
        {
            Console.Error.WriteLine(
                "mgctl: a root directory is required; --admin-certificate-hosts requires --generate-admin-certificate."
            );

            return 2;
        }

        var server = Environment.GetEnvironmentVariable("MOONGATE_SERVER_EXECUTABLE") ??
                     Path.Combine(
                         AppContext.BaseDirectory,
                         OperatingSystem.IsWindows() ? "mgserver.exe" : "mgserver"
                     );

        try
        {
            if (!File.Exists(server))
            {
                throw new FileNotFoundException(
                    "Keep mgctl beside the mgserver executable from the same distribution.",
                    server
                );
            }

            var assembly = typeof(InitCommand).Assembly;
            var header = ResourceUtils.GetEmbeddedResourceString(assembly, "Assets/header.txt")
                .Replace("{Version}", VersionUtils.GetVersion(assembly), StringComparison.Ordinal)
                .Replace("{Codename}", VersionUtils.GetCodename(assembly), StringComparison.Ordinal);
            Console.WriteLine(header);
            Console.WriteLine("Root setup");

            var start = new ProcessStartInfo(server) { UseShellExecute = false };
            start.ArgumentList.Add("--initialize-root");
            start.ArgumentList.Add("--root-directory");
            start.ArgumentList.Add(rootDirectory);

            if (generateAdminCertificate)
            {
                start.ArgumentList.Add("--generate-admin-certificate");
            }

            if (adminCertificateHosts is not null)
            {
                start.ArgumentList.Add("--admin-certificate-hosts");
                start.ArgumentList.Add(adminCertificateHosts);
            }

            using var process = Process.Start(start) ??
                                throw new InvalidOperationException("Could not start root initialization.");

            try
            {
                await process.WaitForExitAsync(cancellationToken);

                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }

                await process.WaitForExitAsync(CancellationToken.None);

                return 130;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"mgctl: {exception.Message}");

            return 1;
        }
    }
}
