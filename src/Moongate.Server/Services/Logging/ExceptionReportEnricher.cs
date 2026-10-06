using System.Reflection;
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

    private readonly ExceptionReportWriter? _writer;

    /// <param name="writer">
    ///     The report writer; null writes no report, and the console shows the message only.
    /// </param>
    public ExceptionReportEnricher(ExceptionReportWriter? writer)
    {
        _writer = writer;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Exception is not { } exception)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ExceptionMessageProperty, MessageOf(exception)));

        if (_writer?.Write(logEvent) is { } report)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ReportFileProperty, report));
        }
    }

    // A wrapper says nothing of its own, so the cause speaks; a blank message gives the type instead.
    private static string MessageOf(Exception exception)
    {
        var shown = exception is AggregateException or TargetInvocationException && exception.InnerException is not null
            ? exception.GetBaseException()
            : exception;

        return string.IsNullOrWhiteSpace(shown.Message) ? shown.GetType().Name : shown.Message;
    }
}
