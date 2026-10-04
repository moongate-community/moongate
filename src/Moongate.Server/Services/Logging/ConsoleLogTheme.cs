using Serilog.Templates.Themes;

namespace Moongate.Server.Services.Logging;

/// <summary>
///     The console's colours, from the website's palette: rune stone indigo around the line, portal blue for
///     information, moonlight for the message, and gold, emerald, amethyst and silver for the values in it.
///     They are 256-colour codes, which screen, tmux and older terminals render too.
/// </summary>
internal static class ConsoleLogTheme
{
    public static TemplateTheme Moongate { get; } = new(
        TemplateTheme.Code,
        new Dictionary<TemplateThemeStyle, string>
        {
            [TemplateThemeStyle.Text] = "\x1b[38;5;255m",
            [TemplateThemeStyle.SecondaryText] = "\x1b[38;5;103m",
            [TemplateThemeStyle.TertiaryText] = "\x1b[38;5;60m",
            [TemplateThemeStyle.Invalid] = "\x1b[1;38;5;220m",
            [TemplateThemeStyle.Null] = "\x1b[38;5;141m",
            [TemplateThemeStyle.Name] = "\x1b[38;5;104m",
            [TemplateThemeStyle.String] = "\x1b[38;5;222m",
            [TemplateThemeStyle.Number] = "\x1b[38;5;115m",
            [TemplateThemeStyle.Boolean] = "\x1b[38;5;141m",
            [TemplateThemeStyle.Scalar] = "\x1b[38;5;152m",
            [TemplateThemeStyle.LevelVerbose] = "\x1b[38;5;60m",
            [TemplateThemeStyle.LevelDebug] = "\x1b[38;5;103m",
            [TemplateThemeStyle.LevelInformation] = "\x1b[38;5;74m",
            [TemplateThemeStyle.LevelWarning] = "\x1b[1;38;5;220m",
            [TemplateThemeStyle.LevelError] = "\x1b[1;38;5;203m",
            [TemplateThemeStyle.LevelFatal] = "\x1b[1;38;5;255;48;5;124m"
        }
    );
}
