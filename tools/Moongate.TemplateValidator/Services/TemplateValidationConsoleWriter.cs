using Moongate.TemplateValidator.Data;
using Spectre.Console;
using System.Reflection;

namespace Moongate.TemplateValidator.Services;

public sealed class TemplateValidationConsoleWriter
{
    private const string ToolName = "Moongate Template Validator";
    private readonly IAnsiConsole _console;

    public TemplateValidationConsoleWriter()
        : this(AnsiConsole.Console) { }

    public TemplateValidationConsoleWriter(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public void Write(string rootDirectory, TemplateValidationResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(result);

        WriteHeader(rootDirectory);

        if (result.ExitCode == 0)
        {
            WriteSuccess(result);

            return;
        }

        WriteFailure(result);
    }

    private void WriteHeader(string rootDirectory)
    {
        var informationalVersion = typeof(TemplateValidationConsoleWriter)
                                   .Assembly
                                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                   ?
                                   .InformationalVersion;

        var version = string.IsNullOrWhiteSpace(informationalVersion) ? "unknown" : informationalVersion;

        _console.MarkupLine($"[grey]{Markup.Escape($"{ToolName} {version}")}[/]");
        _console.MarkupLine($"[grey]Root directory: {Markup.Escape(rootDirectory)}[/]");
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
