namespace Moongate.Abstractions.Data.Email;

/// <summary>
/// Represents a request to render an email template.
/// </summary>
public sealed class EmailTemplateRenderRequest
{
    /// <summary>
    /// Gets or sets the template identifier.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the locale code.
    /// </summary>
    public string Locale { get; set; } = "en";

    /// <summary>
    /// Gets or sets the rendering model.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Model { get; set; } = new Dictionary<string, object?>();
}
