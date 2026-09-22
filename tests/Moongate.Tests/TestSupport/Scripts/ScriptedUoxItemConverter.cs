using System.Diagnostics;

namespace Moongate.Tests.TestSupport.Scripts;

/// <summary>Runs scripts/UoxItemConverter.cs as a real subprocess against a temporary source and
/// destination directory.</summary>
internal sealed class ScriptedUoxItemConverter : IDisposable
{
    /// <summary>Gets the temporary directory the converter reads <c>.dfn</c> files from.</summary>
    public string SourceDirectory { get; }

    /// <summary>Gets the temporary directory the converter writes <c>.toml</c> files under.</summary>
    public string DestinationDirectory { get; }

    public ScriptedUoxItemConverter()
    {
        var root = Path.Combine(Path.GetTempPath(), "moongate-uox-converter-" + Guid.NewGuid().ToString("N"));
        SourceDirectory = Path.Combine(root, "source");
        DestinationDirectory = Path.Combine(root, "destination");
        Directory.CreateDirectory(SourceDirectory);
    }

    /// <summary>Writes one <c>.dfn</c> source file under <see cref="SourceDirectory" />.</summary>
    public string WriteSource(string relativePath, string content)
    {
        var path = Path.Combine(SourceDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        return path;
    }

    /// <summary>
    /// Runs the converter over the whole source directory, returning its exit code and combined output.
    /// With <paramref name="withArguments" /> false, runs it with neither --source nor --destination,
    /// to exercise the usage error path.
    /// </summary>
    public async Task<(int ExitCode, string Output)> RunAsync(bool withArguments = true)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--file");
        start.ArgumentList.Add(ScriptPath());

        if (withArguments)
        {
            start.ArgumentList.Add("--");
            start.ArgumentList.Add("--source");
            start.ArgumentList.Add(SourceDirectory);
            start.ArgumentList.Add("--destination");
            start.ArgumentList.Add(DestinationDirectory);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start");
        process.StandardInput.Close();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await standardOutput + await standardError);
    }

    /// <summary>Locates scripts/UoxItemConverter.cs by walking up from the test output directory to the repository root.</summary>
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

        return Path.Combine(directory.FullName, "scripts", "UoxItemConverter.cs");
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(SourceDirectory)!;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
