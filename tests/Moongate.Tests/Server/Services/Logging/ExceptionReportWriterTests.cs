using Moongate.Server.Services.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace Moongate.Tests.Server.Services.Logging;

public sealed class ExceptionReportWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "moongate-reports-" + Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void Write_AnEventWithAnException_WritesAMarkdownReportReadyForAnIssue()
    {
        var exception = Thrown(new InvalidOperationException("outer", Thrown(new ArgumentException("inner cause"))));

        var path = Writer()
            .Write(
                Event("Command {Name} failed", exception, ("Name", "spawn"), ("SourceContext", "Moongate.Server.Commands"))
            );

        Assert.NotNull(path);
        Assert.Equal(_directory, Path.GetDirectoryName(path));
        Assert.EndsWith(".md", path, StringComparison.Ordinal);
        var report = File.ReadAllText(path);
        Assert.Contains("0.11.0 \"Lilly\"", report, StringComparison.Ordinal);
        Assert.Contains("Command \"spawn\" failed", report, StringComparison.Ordinal);
        Assert.Contains("Moongate.Server.Commands", report, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: outer", report, StringComparison.Ordinal);
        Assert.Contains("System.ArgumentException: inner cause", report, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), report, StringComparison.Ordinal);
        Assert.Contains("```", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_TheSameExceptionAgain_ReusesItsReport()
    {
        var exception = Thrown(new InvalidOperationException("again"));
        var writer = Writer();

        var first = writer.Write(Event("First", exception));
        var second = writer.Write(Event("Second", exception));

        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(_directory));
        Assert.Contains("First", File.ReadAllText(first!), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_AnotherException_GetsItsOwnReport()
    {
        var writer = Writer();

        writer.Write(Event("One", Thrown(new InvalidOperationException("one"))));
        writer.Write(Event("Two", Thrown(new InvalidOperationException("two"))));

        Assert.Equal(2, Directory.GetFiles(_directory).Length);
    }

    [Fact]
    public void Write_AnEventWithoutAnException_WritesNothing()
    {
        Assert.Null(Writer().Write(Event("Nothing wrong", null)));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void Write_ADirectoryThatCannotBeWritten_GivesNoReportAndNeverThrows()
    {
        File.WriteAllText(_directory, "a file where the directory should be");

        Assert.Null(Writer().Write(Event("Failed", Thrown(new InvalidOperationException("x")))));
    }

    [Fact]
    public void Write_PastTheReportCap_WritesNoMore()
    {
        var writer = new ExceptionReportWriter(_directory, "0.11.0", "Lilly", 2);

        var first = writer.Write(Event("One", Thrown(new InvalidOperationException("one"))));
        var second = writer.Write(Event("Two", Thrown(new InvalidOperationException("two"))));
        var third = writer.Write(Event("Three", Thrown(new InvalidOperationException("three"))));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(third);
        Assert.Equal(2, Directory.GetFiles(_directory).Length);
        // A report written before the cap is still reused.
        Assert.Equal(first, writer.Write(Event("One again", Thrown(new InvalidOperationException("one")))));
    }

    [Fact]
    public void Write_AnExceptionHoldingBackticks_KeepsItInOneCodeBlock()
    {
        var path = Writer().Write(Event("Failed", Thrown(new InvalidOperationException("a ``` b"))));

        var report = File.ReadAllText(path!);
        Assert.Contains("````text", report, StringComparison.Ordinal);
    }

    private ExceptionReportWriter Writer()
    {
        return new(_directory, "0.11.0", "Lilly");
    }

    private static LogEvent Event(string template, Exception? exception, params (string Name, object Value)[] properties)
    {
        return new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            exception,
            new MessageTemplateParser().Parse(template),
            properties.Select(property => new LogEventProperty(property.Name, new ScalarValue(property.Value)))
        );
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

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
        else if (File.Exists(_directory))
        {
            File.Delete(_directory);
        }
    }
}
