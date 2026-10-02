using Moongate.Server.Services.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace Moongate.Tests.Server.Services.Logging;

public sealed class ConsoleLogTemplateTests
{
    [Fact]
    public void AnException_ShowsItsMessageAndItsReport_NotTheStack()
    {
        var exception = Thrown(new InvalidOperationException("the bank box is missing"));

        var line = Render(
            "Opening the bank failed",
            exception,
            ("SourceContext", "Moongate.Server.Ultima.Services.BankService"),
            ("ExceptionMessage", exception.Message),
            ("ReportFile", "/srv/moongate/logs/errors/7f3a9c21aa.md")
        );

        Assert.Contains("BankService", line, StringComparison.Ordinal);
        Assert.Contains(
            "Opening the bank failed: the bank box is missing - details: /srv/moongate/logs/errors/7f3a9c21aa.md (paste it into a GitHub issue)",
            line,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(" at ", line, StringComparison.Ordinal);
        Assert.Single(line.TrimEnd('\n').Split('\n'));
    }

    [Fact]
    public void AnEventWithoutAnException_IsOneLine()
    {
        var line = Render("All fine", null);

        Assert.EndsWith("Moongate                     | All fine\n", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExceptionWithoutItsProperties_StillShowsTheException()
    {
        var line = Render("Failed", new InvalidOperationException("enricher missing"));

        Assert.Contains("enricher missing", line, StringComparison.Ordinal);
    }

    private static string Render(string template, Exception? exception, params (string Name, object Value)[] properties)
    {
        var logEvent = new LogEvent(
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero),
            LogEventLevel.Error,
            exception,
            new MessageTemplateParser().Parse(template),
            properties.Select(property => new LogEventProperty(property.Name, new ScalarValue(property.Value)))
        );
        var output = new StringWriter();
        ConsoleLogTemplate.Create(null).Format(logEvent, output);

        return output.ToString();
    }

    private static T Thrown<T>(T exception) where T : Exception
    {
        try
        {
            throw exception;
        }
        catch (T caught)
        {
            return caught;
        }
    }
}
