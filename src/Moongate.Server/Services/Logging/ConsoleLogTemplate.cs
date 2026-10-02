using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Moongate.Server.Services.Logging;

/// <summary>
///     The console's log line: time, level, source and message, then for an exception only its message and its report
///     file, never the stack, which the report and the .clef logs keep.
/// </summary>
internal static class ConsoleLogTemplate
{
    public static ExpressionTemplate Create(TemplateTheme? theme)
    {
        return new(
            "{@t:HH:mm:ss.fff} {@l:u3} " +
            "{Coalesce(Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1), 'Moongate'),-28}" +
            " | {@m}" +
            "{#if ExceptionMessage is not null}: {ExceptionMessage}{#else if @x is not null}: {@x}{#end}" +
            "{#if ReportFile is not null} - details: {ReportFile} (paste it into a GitHub issue){#end}\n",
            theme: theme
        );
    }
}
