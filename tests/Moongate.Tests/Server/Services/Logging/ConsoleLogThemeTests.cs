using System.Text.RegularExpressions;
using Moongate.Server.Services.Logging;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Moongate.Tests.Server.Services.Logging;

public sealed class ConsoleLogThemeTests
{
    [Theory]
    [InlineData(LogEventLevel.Verbose, "\x1b[38;5;60mVRB")]
    [InlineData(LogEventLevel.Debug, "\x1b[38;5;103mDBG")]
    [InlineData(LogEventLevel.Information, "\x1b[38;5;74mINF")]
    [InlineData(LogEventLevel.Warning, "\x1b[1;38;5;220mWRN")]
    [InlineData(LogEventLevel.Error, "\x1b[1;38;5;203mERR")]
    [InlineData(LogEventLevel.Fatal, "\x1b[1;38;5;255;48;5;124mFTL")]
    public void ALevel_HasItsColour(LogEventLevel level, string expected)
    {
        var line = Render(ConsoleLogTheme.Moongate, level, "All fine");

        Assert.Contains(expected, line, StringComparison.Ordinal);
    }

    [Fact]
    public void TheValuesInAMessage_AreColouredByTheirKind()
    {
        (string Name, object Value)[] properties =
        [
            ("Count", 12),
            ("Region", "britain_graveyard"),
            ("Initial", true)
        ];

        var line = Render(
            ConsoleLogTheme.Moongate,
            LogEventLevel.Information,
            "Spawned {Count} mobiles in {Region}, initial: {Initial}",
            properties
        );

        Assert.Contains("\x1b[38;5;255mSpawned ", line, StringComparison.Ordinal);
        Assert.Contains("\x1b[38;5;115m12", line, StringComparison.Ordinal);
        Assert.Contains("\x1b[38;5;222mbritain_graveyard", line, StringComparison.Ordinal);
        Assert.Contains("\x1b[38;5;141mTrue", line, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTheme_OnlyAddsColours_TheTextIsTheSame()
    {
        (string Name, object Value)[] properties =
        [
            ("Count", 12),
            ("Region", "britain_graveyard")
        ];
        const string template = "Spawned {Count} mobiles in {Region}";

        var plain = Render(null, LogEventLevel.Warning, template, properties);
        var themed = Render(ConsoleLogTheme.Moongate, LogEventLevel.Warning, template, properties);

        Assert.NotEqual(plain, themed);
        Assert.Equal(plain, Regex.Replace(themed, "\x1b\\[[0-9;]*m", ""));
    }

    private static string Render(
        TemplateTheme? theme,
        LogEventLevel level,
        string template,
        params (string Name, object Value)[] properties
    )
    {
        var logEvent = new LogEvent(
            new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero),
            level,
            null,
            new MessageTemplateParser().Parse(template),
            properties.Select(property => new LogEventProperty(property.Name, new ScalarValue(property.Value)))
        );
        var output = new StringWriter();

        // A test's output is redirected, where a template drops its theme unless told to keep it.
        var line = new ExpressionTemplate("{@l:u3} {@m}\n", theme: theme, applyThemeWhenOutputIsRedirected: true);
        line.Format(logEvent, output);

        return output.ToString();
    }
}
