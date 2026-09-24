#:package Npgsql@5.0.18

using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Npgsql;

namespace Moongate.Tools.Internal;

internal static class VerifyNuGetConsumers
{
    private static readonly Dictionary<string, string> ExpectedOutput = new(StringComparer.Ordinal)
    {
        ["Moongate.Admin.Contracts"] = "moongate.admin.v1:portable",
        ["Moongate.Core"] = "0x00000001: 100, 200, 5",
        ["Moongate.Network"] = "TCP listener started and stopped.",
        ["Moongate.Network.Packets"] = "73:42",
        ["Moongate.Persistence"] = "Mario",
        ["Moongate.Persistence.Migrations"] = "World",
        ["Moongate.Scripting"] = "Hello, Moongate!",
        ["Moongate.Server.Core"] = "Started",
        ["Moongate.Ultima"] = "2x2"
    };

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: VerifyNuGetConsumers <repository-root> <package-directory>");
            return 2;
        }

        string? workspace = null;
        try
        {
            var repository = Path.GetFullPath(args[0]);
            var packages = Path.GetFullPath(args[1]);
            var version = XDocument.Load(Path.Combine(repository, "Directory.Build.props"))
                .Descendants("Version").Single().Value;
            workspace = Directory.CreateTempSubdirectory("moongate-nuget-").FullName;
            var relative = Path.GetRelativePath(repository, workspace);
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The consumer workspace must be outside the repository. Configure a different temporary directory.");
            }
            var cache = Path.Combine(workspace, "packages");
            var config = Path.Combine(workspace, "NuGet.Config");
            WriteNuGetConfig(config, packages);
            Console.WriteLine($"Consumer workspace: {workspace}");

            foreach (var (id, expectedOutput) in ExpectedOutput)
            {
                Console.WriteLine($"Checking {id} from the local feed...");
                var directory = Path.Combine(workspace, id);
                Directory.CreateDirectory(directory);
                ExtractExamples(Path.Combine(repository, "src", id, "README.md"), directory, id);
                WriteProject(repository, directory, id, version);
                await RunDotnetAsync(directory, cache, null, "restore", "Consumer.csproj", "--configfile", config);
                await RunDotnetAsync(directory, cache, null, "build", "Consumer.csproj", "-c", "Release", "--no-restore");
                var stdout = id == "Moongate.Persistence"
                    ? await RunPersistenceConsumerAsync(directory, cache)
                    : await RunDotnetAsync(directory, cache, null,
                        "run", "--project", "Consumer.csproj", "-c", "Release", "--no-build", "--no-restore");
                var lastLine = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
                if (lastLine != expectedOutput)
                {
                    throw new InvalidDataException($"{id}: expected '{expectedOutput}', received:\n{stdout}");
                }
                Console.WriteLine($"PASS {id}: {expectedOutput}");
            }

            Directory.Delete(workspace, recursive: true);
            workspace = null;
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Consumer verification failed: {exception.Message}");
            if (workspace is not null)
            {
                Console.Error.WriteLine($"Workspace retained for diagnosis: {workspace}");
            }
            return 1;
        }
    }

    private static void WriteNuGetConfig(string path, string packages)
    {
        new XDocument(new XElement("configuration",
            new XElement("packageSources",
                new XElement("clear"),
                new XElement("add", new XAttribute("key", "local"), new XAttribute("value", packages)),
                new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
            new XElement("packageSourceMapping",
                new XElement("clear"),
                new XElement("packageSource", new XAttribute("key", "local"),
                    new XElement("package", new XAttribute("pattern", "Moongate.*"))),
                new XElement("packageSource", new XAttribute("key", "nuget.org"),
                    new XElement("package", new XAttribute("pattern", "*")))))).Save(path);
    }

    private static void ExtractExamples(string readmePath, string directory, string id)
    {
        const string pattern = @"<!-- nuget-smoke:(?<file>[A-Za-z][A-Za-z0-9]*\.cs) -->\s*```csharp\r?\n(?<code>[\s\S]*?)\r?\n```";
        var matches = Regex.Matches(File.ReadAllText(readmePath), pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        var expected = new HashSet<string>(StringComparer.Ordinal) { "Program.cs" };
        if (id == "Moongate.Persistence")
        {
            expected.Add("Player.cs");
        }
        var actual = matches.Select(match => match.Groups["file"].Value).ToHashSet(StringComparer.Ordinal);
        if (matches.Count != expected.Count || !actual.SetEquals(expected))
        {
            throw new InvalidDataException($"{id}: expected exactly the documented examples {string.Join(", ", expected)}.");
        }
        foreach (Match match in matches)
        {
            File.WriteAllText(Path.Combine(directory, match.Groups["file"].Value), match.Groups["code"].Value + Environment.NewLine);
        }
    }

    private static void WriteProject(string repository, string directory, string id, string version)
    {
        var references = new XElement("ItemGroup",
            new XElement("PackageReference", new XAttribute("Include", id), new XAttribute("Version", $"[{version}]")));
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("OutputType", "Exe"),
                new XElement("TargetFramework", "net10.0"),
                new XElement("ImplicitUsings", "enable"),
                new XElement("Nullable", "enable"),
                new XElement("IsPackable", "false")), references)).Save(Path.Combine(directory, "Consumer.csproj"));
    }

    private static async Task<string> RunPersistenceConsumerAsync(string directory, string cache)
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("MOONGATE_TEST_POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new InvalidOperationException(
                "MOONGATE_TEST_POSTGRES_CONNECTION_STRING is required to run the Moongate.Persistence package example.");
        }

        var databaseName = $"moongate_test_nuget_{Guid.NewGuid():N}";
        var quotedDatabaseName = new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {quotedDatabaseName}", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var runtime = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            return await RunDotnetAsync(directory, cache,
                new Dictionary<string, string> { ["MOONGATE_PERSISTENCE_DATABASE"] = runtime.ConnectionString },
                "run", "--project", "Consumer.csproj", "-c", "Release", "--no-build", "--no-restore");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {quotedDatabaseName} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> RunDotnetAsync(
        string workingDirectory,
        string cacheDirectory,
        IReadOnlyDictionary<string, string>? environment = null,
        params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment["NUGET_PACKAGES"] = cacheDirectory;
        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                start.Environment[name] = value;
            }
        }
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync();
            throw new TimeoutException($"dotnet {arguments[0]} timed out in {workingDirectory}.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {arguments[0]} failed in {workingDirectory}:\n{stdout}\n{stderr}");
        }
        return stdout;
    }
}
