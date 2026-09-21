using System.Diagnostics;

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine("Usage: mgboot <root-directory>");
    Console.WriteLine("Creates default configuration and copies bundled base migrations without starting the server or connecting to PostgreSQL.");
    return 0;
}

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]) || args[0].StartsWith('-'))
{
    Console.Error.WriteLine("Usage: mgboot <root-directory>");
    return 2;
}

var server = Environment.GetEnvironmentVariable("MOONGATE_SERVER_EXECUTABLE") ??
             Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Moongate.Server.exe" : "Moongate.Server");
try
{
    if (!File.Exists(server))
    {
        throw new FileNotFoundException("Keep mgboot beside the Moongate.Server executable from the same distribution.", server);
    }

    var start = new ProcessStartInfo(server) { UseShellExecute = false };
    start.ArgumentList.Add("--initialize-root");
    start.ArgumentList.Add("--root-directory");
    start.ArgumentList.Add(args[0]);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start root initialization.");
    await process.WaitForExitAsync();
    return process.ExitCode;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"mgboot: {exception.Message}");
    return 1;
}
