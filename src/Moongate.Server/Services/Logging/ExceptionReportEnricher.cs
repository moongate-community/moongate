using Serilog.Core;
using Serilog.Events;

namespace Moongate.Server.Services.Logging;

/// <summary>
///     Gives every logged exception a report file and two properties for the console, which shows the message and the
///     report instead of the whole stack: <c>ExceptionMessage</c> and <c>ReportFile</c>.
/// </summary>
internal sealed class ExceptionReportEnricher : ILogEventEnricher
{
    public const string ExceptionMessageProperty = "ExceptionMessage";
    public const string ReportFileProperty = "ReportFile";

    private readonly ExceptionReportWriter _writer;

    public ExceptionReportEnricher(ExceptionReportWriter writer)
    {
        _writer = writer;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Exception is not { } exception)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ExceptionMessageProperty, exception.Message));

        if (_writer.Write(logEvent) is { } report)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ReportFileProperty, report));
        }
    }
}
