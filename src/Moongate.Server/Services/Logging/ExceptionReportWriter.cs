using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Serilog.Events;

namespace Moongate.Server.Services.Logging;

/// <summary>
///     Writes the report of a logged exception into <c>logs/errors/&lt;id&gt;.md</c>, in Markdown ready to paste into a
///     GitHub issue: the version, the system, the time, the message and the whole exception. The id hashes the
///     exception, so the same one logged again reuses its report.
/// </summary>
internal sealed class ExceptionReportWriter
{
    private readonly Lock _sync = new();
    private readonly string _directory;
    private readonly string _version;
    private readonly string _codename;

    public ExceptionReportWriter(string directory, string version, string codename)
    {
        _directory = directory;
        _version = version;
        _codename = codename;
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

                ```text
                {details}
                ```

                """;
    }
}
