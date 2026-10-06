using Moongate.Core.Utils;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     What every pass of the converter writes the same way: a TOML file under a comment header, the report of dropped
///     values, and the verification errors found reading the output back.
/// </summary>
internal static class ConverterOutput
{
    /// <summary>
    ///     Writes <paramref name="model" /> as TOML under <paramref name="header" />, creating the folder.
    /// </summary>
    public static void WriteToml<T>(string path, string header, T model)
    {
        // Safe: the path is built with GetFullPath and always has a parent directory.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, header + TomlUtils.Serialize(model));
    }

    /// <summary>
    ///     Prints each reason of <paramref name="report" /> with how often it happened.
    /// </summary>
    public static void WriteReport(TextWriter output, ConversionReport report)
    {
        foreach (var (reason, count) in report.Lines)
        {
            output.WriteLine($"  {count} x {reason}");
        }
    }

    /// <summary>
    ///     Prints every verification error and their count; returns the exit code, 1 when there is any, else 0.
    /// </summary>
    public static int ReportErrors(TextWriter error, IReadOnlyList<string> errors, string what)
    {
        foreach (var verificationError in errors)
        {
            error.WriteLine($"Verification failed: {verificationError}");
        }

        if (errors.Count == 0)
        {
            return 0;
        }

        error.WriteLine($"{errors.Count} verification error(s) found reading the converted {what} back.");

        return 1;
    }
}
