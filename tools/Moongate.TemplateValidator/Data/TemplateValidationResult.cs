namespace Moongate.TemplateValidator.Data;

public sealed class TemplateValidationResult
{
    public int ExitCode { get; init; }

    public int ItemTemplateCount { get; init; }

    public int MobileTemplateCount { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Errors { get; init; } = [];
}
