using ConsoleAppFramework;
using Moongate.TemplateValidator.Services;

namespace Moongate.TemplateValidator.Commands;

/// <summary>
/// Validates all supported shard templates under a Moongate root directory.
/// </summary>
public sealed class TemplateValidateCommand
{
    private TemplateValidationRunner _runner = new();
    private TemplateValidationConsoleWriter _consoleWriter = new();

    public TemplateValidationRunner Runner
    {
        get => _runner;
        init => _runner = value ?? throw new ArgumentNullException(nameof(value));
    }

    public TemplateValidationConsoleWriter ConsoleWriter
    {
        get => _consoleWriter;
        init => _consoleWriter = value ?? throw new ArgumentNullException(nameof(value));
    }

    public TemplateValidateCommand()
    {
    }

    /// <summary>
    /// Validate the shard templates under a root directory.
    /// </summary>
    /// <param name="rootDirectory">Path to the shard root directory.</param>
    [Command("")]
    public async Task<int> RunAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        var result = await _runner.ValidateAsync(rootDirectory, cancellationToken);
        _consoleWriter.Write(result);

        return result.ExitCode;
    }
}
