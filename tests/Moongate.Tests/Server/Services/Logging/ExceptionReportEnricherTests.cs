using Moongate.Server.Services.Logging;
using Serilog;
using Serilog.Events;
using Moongate.Tests.TestSupport.Console;

namespace Moongate.Tests.Server.Services.Logging;

public sealed class ExceptionReportEnricherTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "moongate-reports-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingLogEventSink _sink = new(new RecordingConsoleDriver());

    [Fact]
    public void AnEventWithAnException_GetsItsMessageAndTheReportPath()
    {
        using (var logger = Logger())
        {
            logger.Error(new InvalidOperationException("the bank box is missing"), "Opening the bank failed");
        }

        var logEvent = Assert.Single(_sink.Events);
        Assert.Equal("the bank box is missing", ((ScalarValue)logEvent.Properties["ExceptionMessage"]).Value);
        var report = (string)((ScalarValue)logEvent.Properties["ReportFile"]).Value!;
        Assert.True(File.Exists(report));
    }

    [Fact]
    public void AnEventWithoutAnException_IsLeftAlone()
    {
        using (var logger = Logger())
        {
            logger.Information("All fine");
        }

        var logEvent = Assert.Single(_sink.Events);
        Assert.False(logEvent.Properties.ContainsKey("ExceptionMessage"));
        Assert.False(logEvent.Properties.ContainsKey("ReportFile"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private Serilog.Core.Logger Logger()
    {
        return new LoggerConfiguration()
               .Enrich.With(new ExceptionReportEnricher(new ExceptionReportWriter(_directory, "0.11.0", "Lilly")))
               .WriteTo.Sink(_sink)
               .CreateLogger();
    }
}
