using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Console.Internal.Logging;
using Moongate.Server.Services.Logging.Internal;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Compact;

namespace Moongate.Server.Services.Logging;

/// <summary>
///     Builds the server's logger: the console behind the prompt, showing an exception as its message and its report,
///     and with file logging the .clef logs, with the full exception, and the reports in <c>logs/errors</c>.
/// </summary>
internal static class ServerLoggerFactory
{
    /// <param name="consoleWriter">Where the console lines go; null writes them to the terminal, with colours.</param>
    public static Logger Create(
        IConsolePromptService prompt,
        string logsDirectory,
        bool logToFile,
        string version,
        string codename,
        TextWriter? consoleWriter = null
    )
    {
        // Pass-through: the outer logger owns level policy.
        var console = new LoggerConfiguration().MinimumLevel.Verbose();
        var consoleLogger = (consoleWriter is null
                                 ? console.WriteTo.Console(ConsoleLogTemplate.Create(ConsoleLogTheme.Moongate))
                                 : console.WriteTo.Sink(new TextWriterLogSink(ConsoleLogTemplate.Create(null), consoleWriter)))
            .CreateLogger();

        // Reports go to disk only when logs do: a shard that turned file logging off writes nothing.
        var reports = logToFile ? new ExceptionReportWriter(Path.Combine(logsDirectory, "errors"), version, codename) : null;
        var configuration = new LoggerConfiguration()
                            .Enrich
                            .With(new ExceptionReportEnricher(reports))
                            .WriteTo
                            .Sink(new PromptAwareConsoleSink(prompt, consoleLogger));

        if (logToFile)
        {
            configuration = configuration.WriteTo.File(
                // One JSON object per line, keeping the message template and its properties separate so a log reader
                // can group events by template; .clef is the extension log shippers recognise.
                new CompactJsonFormatter(),
                Path.Combine(logsDirectory, "moongate-.clef"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                retainedFileCountLimit: 30
            );
        }

        return configuration.CreateLogger();
    }
}
