namespace Moongate.Email.Data;

/// <summary>
/// Configures file-based email template rendering.
/// </summary>
public sealed class EmailTemplateOptions
{
    /// <summary>
    /// Gets or sets the root directory containing template files.
    /// </summary>
    public string TemplatesRootPath { get; set; }

    /// <summary>
    /// Gets or sets the fallback locale.
    /// </summary>
    public string FallbackLocale { get; set; } = "en";
}
