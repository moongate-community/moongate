using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Serilog.Events;

namespace Moongate.Server.Services.Logging;

/// <summary>
///     Writes the report of a logged exception into <c>logs/errors/&lt;id&gt;.md</c>, in Markdown ready to paste into a
///     GitHub issue: the version, the system, the time, the message and the whole exception. The id hashes the
///     exception, so the same one logged again reuses its report; past <see cref="DefaultMaxReports" /> reports no new
///     one is written, so exceptions whose messages vary cannot fill the disk.
/// </summary>
internal sealed class ExceptionReportWriter
{
    public const int DefaultMaxReports = 500;

    private readonly Lock _sync = new();
    private readonly string _directory;
    private readonly string _version;
    private readonly string _codename;
    private readonly int _maxReports;

    public ExceptionReportWriter(string directory, string version, string codename, int maxReports = DefaultMaxReports)
    {
        _directory = directory;
        _version = version;
        _codename = codename;
        _maxReports = maxReports;
    }

    /// <summary>
    ///     Writes the report of <paramref name="logEvent" />'s exception, unless the same exception has one already;
    ///     gets its path, or null for an event without an exception or a report that could not be written. It never
    ///     throws: it runs while logging.
    /// </summary>
    public string? Write(LogEvent logEvent)
    {
        if (logEvent.Exception is not { } exception)
        {
            return null;
        }

        try
        {
            var details = exception.ToString();
            var id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(details)), 0, 5);
            var path = Path.Combine(_directory, id + ".md");

            lock (_sync)
            {
                if (!File.Exists(path))
                {
                    if (Directory.Exists(_directory) && Directory.GetFiles(_directory, "*.md").Length >= _maxReports)
                    {
                        return null;
                    }

                    Directory.CreateDirectory(_directory);
                    File.WriteAllText(path, Report(logEvent, details));
                }
            }

            return path;
        }
        catch (Exception exceptionWhileWriting) when (exceptionWhileWriting is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string Report(LogEvent logEvent, string details)
    {
        var source = logEvent.Properties.TryGetValue("SourceContext", out var value) && value is ScalarValue { Value: string name }
            ? name
            : "Moongate";

        // A fence longer than any run of backticks in the exception keeps it in one code block.
        var fence = new string('`', Math.Max(3, LongestBacktickRun(details) + 1));

        return $"""
                ## Moongate exception

                | | |
                | --- | --- |
                | Version | {_version} "{_codename}" |
                | System | {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}) |
                | .NET | {RuntimeInformation.FrameworkDescription} |
                | Time (UTC) | {logEvent.Timestamp.UtcDateTime:yyyy-MM-dd HH:mm:ss} |
                | Source | {source} |
                | Level | {logEvent.Level} |

                **Message:** {logEvent.RenderMessage()}

                {fence}text
                {details}
                {fence}

                """;
    }

    private static int LongestBacktickRun(string text)
    {
        var longest = 0;
        var run = 0;

        foreach (var character in text)
        {
            run = character == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }
}
