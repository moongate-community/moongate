using Moongate.TemplateValidator.Data;
using Spectre.Console;

namespace Moongate.TemplateValidator.Services;

public sealed class TemplateValidationConsoleWriter
{
    private readonly IAnsiConsole _console;

    public TemplateValidationConsoleWriter()
        : this(AnsiConsole.Console)
    {
    }

    public TemplateValidationConsoleWriter(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public void Write(TemplateValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExitCode == 0)
        {
            WriteSuccess(result);

            return;
        }

        WriteFailure(result);
    }

    private void WriteFailure(TemplateValidationResult result)
    {
        _console.MarkupLine($"[red]{Markup.Escape(result.Summary)}[/]");

        foreach (var error in result.Errors)
        {
            _console.MarkupLine($"[red]- {Markup.Escape(error)}[/]");
        }
    }

    private void WriteSuccess(TemplateValidationResult result)
        => _console.MarkupLine($"[green]{Markup.Escape(result.Summary)}[/]");
}
